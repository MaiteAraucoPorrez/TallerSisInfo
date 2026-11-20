using Breaku.Application.Comunidades;
using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Tests;

/// <summary>Aviso a quien creó una comunidad cuando alguien se suma o se baja (migración 031).</summary>
public class AvisoCreadoraTests
{
    private const string Confirmado = Participacion.EstadoConfirmado;
    private const string Rechazado = Participacion.EstadoRechazado;
    private const string Pendiente = Participacion.EstadoPendiente;

    [Fact]
    public void Sumarse_avisa_con_el_cupo_nuevo()
        => Assert.Equal("Ana se sumó a «jugar vóley». Van 3 de 6.",
            ReglasAsistencia.MensajeParaCreadora(Pendiente, Confirmado, true, "Ana", "jugar vóley", 3, 6));

    [Fact]
    public void Unirse_desde_Descubrir_sin_invitacion_tambien_avisa()
        => Assert.NotNull(ReglasAsistencia.MensajeParaCreadora(null, Confirmado, true, "Ana", "jugar vóley", 2, 6));

    [Fact]
    public void Bajarse_despues_de_confirmar_avisa()
        => Assert.Equal("Ana ya no va a «jugar vóley». Van 2 de 6.",
            ReglasAsistencia.MensajeParaCreadora(Confirmado, Rechazado, true, "Ana", "jugar vóley", 2, 6));

    [Fact]
    public void Rechazar_una_invitacion_sin_haber_confirmado_no_avisa()
        => Assert.Null(ReglasAsistencia.MensajeParaCreadora(Pendiente, Rechazado, true, "Ana", "jugar vóley", 2, 6));

    [Fact]
    public void Repetir_la_misma_respuesta_no_avisa()
    {
        Assert.Null(ReglasAsistencia.MensajeParaCreadora(Confirmado, Confirmado, false, "Ana", "jugar vóley", 3, 6));
        Assert.Null(ReglasAsistencia.MensajeParaCreadora(Confirmado, Confirmado, true, "Ana", "jugar vóley", 3, 6));
    }

    [Fact]
    public void Sin_nombre_dice_alguien()
        => Assert.StartsWith("Alguien se sumó", ReglasAsistencia.MensajeParaCreadora(Pendiente, Confirmado, true, "  ", "cartas", 2, 4));

    [Fact]
    public void Actividad_muy_larga_se_recorta_al_largo_del_aviso()
    {
        var mensaje = ReglasAsistencia.MensajeParaCreadora(Pendiente, Confirmado, true, "Ana", new string('x', 300), 2, 4)!;

        Assert.True(mensaje.Length <= ReglasAsistencia.MaximoLargoAviso);
        Assert.Contains("…»", mensaje);
        Assert.EndsWith("Van 2 de 4.", mensaje);
    }

    // ---------- Servicio ----------

    private sealed class Repositorio(ContextoAsistencia contexto) : IAsistenciaRepository
    {
        public CambioAsistencia? Cambio { get; private set; }

        public Task ResponderAsync(int comunidadId, int usuarioId, DateTime ahoraUtc, Func<ContextoAsistencia, CambioAsistencia> decidir,
            CancellationToken ct = default)
        {
            Cambio = decidir(contexto);
            return Task.CompletedTask;
        }

        public Task<ParticipantesComunidad?> ObtenerParticipantesAsync(int comunidadId, CancellationToken ct = default)
            => Task.FromResult<ParticipantesComunidad?>(null);
    }

    private sealed class Reloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 3, 13, 0, 0, TimeSpan.Zero);
    }

    private static ContextoAsistencia Contexto(string? miEstado, int confirmados) => new(new ComunidadEfimera
    {
        Id = 12, CreadorId = 2, Actividad = "jugar cartas", MaxParticipantes = 4,
        Inicio = new DateTime(2026, 11, 3, 14, 0, 0, DateTimeKind.Utc),
        Fin = new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc),
        Estado = ComunidadEfimera.EstadoActiva
    }, miEstado, false, confirmados, null, [], "Lucía Rojas Pérez");

    [Fact]
    public async Task Confirmar_deja_el_aviso_para_quien_creo_con_el_nombre_corto()
    {
        var repo = new Repositorio(Contexto(Pendiente, 1));

        await new AsistenciaService(repo, new Reloj()).ResponderAsync(5, 12, true);

        Assert.Equal($"{ReglasDescubrir.NombreCorto("Lucía Rojas Pérez")} se sumó a «jugar cartas». Van 2 de 4.", repo.Cambio!.AvisoCreadora);
    }

    [Fact]
    public async Task Bajarse_deja_el_aviso_con_un_confirmado_menos()
    {
        var repo = new Repositorio(Contexto(Confirmado, 3));

        await new AsistenciaService(repo, new Reloj()).ResponderAsync(5, 12, false);

        Assert.EndsWith("ya no va a «jugar cartas». Van 2 de 4.", repo.Cambio!.AvisoCreadora);
    }

    [Fact]
    public async Task Rechazar_la_invitacion_no_deja_aviso()
    {
        var repo = new Repositorio(Contexto(Pendiente, 1));

        await new AsistenciaService(repo, new Reloj()).ResponderAsync(5, 12, false);

        Assert.Null(repo.Cambio!.AvisoCreadora);
    }
}
