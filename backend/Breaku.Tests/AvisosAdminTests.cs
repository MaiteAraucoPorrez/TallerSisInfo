using Breaku.Application.Notificaciones;
using Breaku.Application.Reportes;
using Breaku.Application.TiempoReal;
using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Tests;

/// <summary>Campanita del admin: reportes nuevos y eventos por revisar (migración 030).</summary>
public class AvisosAdminTests
{
    /// <summary>Lo que se pidió avisar o cerrar, en orden.</summary>
    internal sealed class AvisosFalsos : IAvisosAdmin
    {
        public List<string> Llamadas { get; } = [];
        public Task ReporteNuevoAsync(int reporteId, string motivo, string? descripcion, CancellationToken ct = default)
        { Llamadas.Add($"reporte-nuevo:{reporteId}:{motivo}"); return Task.CompletedTask; }
        public Task ReporteRevisadoAsync(int reporteId, CancellationToken ct = default)
        { Llamadas.Add($"reporte-revisado:{reporteId}"); return Task.CompletedTask; }
        public Task EventoPorRevisarAsync(int eventoId, string titulo, string? motivo, CancellationToken ct = default)
        { Llamadas.Add($"evento-por-revisar:{eventoId}"); return Task.CompletedTask; }
        public Task EventoRevisadoAsync(int eventoId, CancellationToken ct = default)
        { Llamadas.Add($"evento-revisado:{eventoId}"); return Task.CompletedTask; }
    }

    private sealed class RepoAvisos : IAvisosAdminRepository
    {
        public bool Falla { get; init; }
        public List<(string Tipo, ObjetoAvisoAdmin Objeto, int Id, string? Mensaje)> Hechos { get; } = [];
        public Task<IReadOnlyList<int>> AvisarAsync(string tipo, ObjetoAvisoAdmin objeto, int objetoId, string mensaje, CancellationToken ct = default)
        {
            if (Falla) throw new InvalidOperationException("Check constraint 'ck_notif_tipo' is violated.");
            Hechos.Add((tipo, objeto, objetoId, mensaje));
            return Task.FromResult<IReadOnlyList<int>>([2, 9]);
        }
        public Task<IReadOnlyList<int>> CerrarAsync(string tipo, ObjetoAvisoAdmin objeto, int objetoId, CancellationToken ct = default)
        {
            Hechos.Add((tipo, objeto, objetoId, null));
            return Task.FromResult<IReadOnlyList<int>>([]);
        }
    }

    private sealed class EnVivoFalso : ITiempoReal
    {
        public List<int> Avisados { get; } = [];
        public Task AvisosCambiaronAsync(IReadOnlyCollection<int> usuarioIds, CancellationToken ct = default)
        { Avisados.AddRange(usuarioIds); return Task.CompletedTask; }
        public Task ComunidadCreadaAsync(int comunidadId, CancellationToken ct = default) => Task.CompletedTask;
        public Task AvisosDeComunidadAsync(int comunidadId, CancellationToken ct = default) => Task.CompletedTask;
        public Task ComunidadActualizadaAsync(int comunidadId, string estado, int confirmados, CancellationToken ct = default) => Task.CompletedTask;
    }

    // ── Textos ──

    [Fact]
    public void El_aviso_de_reporte_dice_el_motivo_y_cita_la_descripcion()
    {
        var m = ReglasAvisosAdmin.MensajeReporteNuevo(ReglasReporte.MotivoLugarIncorrecto, "  Dice que abre a las 7\ny abre a las 9 ");
        Assert.Equal("Nuevo reporte «Información de un lugar». «Dice que abre a las 7 y abre a las 9»", m);
    }

    [Fact]
    public void Un_aviso_largo_se_recorta_a_200_caracteres()
    {
        var m = ReglasAvisosAdmin.MensajeEventoPorRevisar(new string('T', 150), new string('x', 500));
        Assert.True(m.Length <= ReglasAvisosAdmin.LargoMaximo);
        Assert.Contains("…", m);
        Assert.Contains("espera tu revisión", m);
    }

    [Fact]
    public void Sin_motivo_el_aviso_de_evento_no_lleva_cita()
        => Assert.Equal("«Feria de ciencias» espera tu revisión.", ReglasAvisosAdmin.MensajeEventoPorRevisar(" Feria de ciencias ", null));

    // ── Servicio ──

    [Fact]
    public async Task Un_reporte_nuevo_avisa_a_los_admins_en_vivo()
    {
        var repo = new RepoAvisos();
        var enVivo = new EnVivoFalso();
        await new AvisosAdminService(repo, enVivo).ReporteNuevoAsync(12, ReglasReporte.MotivoErrorApp, "No carga el horario");

        var hecho = Assert.Single(repo.Hechos);
        Assert.Equal((Notificacion.TipoReporteNuevo, ObjetoAvisoAdmin.Reporte, 12), (hecho.Tipo, hecho.Objeto, hecho.Id));
        Assert.Equal([2, 9], enVivo.Avisados);
    }

    [Fact]
    public async Task Cerrar_un_evento_revisado_apunta_al_evento()
    {
        var repo = new RepoAvisos();
        await new AvisosAdminService(repo, new EnVivoFalso()).EventoRevisadoAsync(5);

        var hecho = Assert.Single(repo.Hechos);
        Assert.Equal((Notificacion.TipoEventoPorRevisar, ObjetoAvisoAdmin.Evento, 5), (hecho.Tipo, hecho.Objeto, hecho.Id));
    }

    [Fact]
    public async Task Si_la_base_no_tiene_la_030_el_aviso_falla_en_silencio()
    {
        var enVivo = new EnVivoFalso();
        await new AvisosAdminService(new RepoAvisos { Falla = true }, enVivo).EventoPorRevisarAsync(5, "Feria", null);
        Assert.Empty(enVivo.Avisados);
    }

    // ── Reportes ──

    private sealed class RepoReportes : IReporteRepository
    {
        public Task<List<LugarReportableDto>> ListarLugaresAsync(CancellationToken ct = default) => Task.FromResult(new List<LugarReportableDto>());
        public Task<bool> ObjetoExisteAsync(string objeto, int id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> ContarPendientesAsync(int usuarioId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<bool> TienePendienteSobreAsync(int usuarioId, string objeto, int id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CrearAsync(int usuarioId, string motivo, int? lugarId, int? comunidadId, int? eventoId, string descripcion,
            CancellationToken ct = default) => Task.FromResult(31);
        public Task<List<MiReporteDto>> ListarMiosAsync(int usuarioId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Enviar_un_reporte_avisa_a_los_admins()
    {
        var avisos = new AvisosFalsos();
        await new ReporteService(new RepoReportes(), avisos).CrearAsync(1, new NuevoReporteDto("ERROR_APP", null, "El botón de guardar no responde"));
        Assert.Equal(["reporte-nuevo:31:ERROR_APP"], avisos.Llamadas);
    }

    [Fact]
    public async Task Un_reporte_invalido_no_avisa()
    {
        var avisos = new AvisosFalsos();
        await Assert.ThrowsAsync<Breaku.Application.Horarios.ReglaNegocioException>(
            () => new ReporteService(new RepoReportes(), avisos).CrearAsync(1, new NuevoReporteDto("ERROR_APP", null, "corto")));
        Assert.Empty(avisos.Llamadas);
    }
}
