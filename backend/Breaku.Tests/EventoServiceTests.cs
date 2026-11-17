using Breaku.Application.Eventos;
using Breaku.Application.Horarios;
using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Tests;

public sealed class EventoServiceTests
{
    private const int Usuario = 7;
    private static readonly DateTimeOffset Ahora = new(2026, 11, 17, 16, 0, 0, TimeSpan.Zero);

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class RepositorioFalso : IEventoRepository
    {
        public List<Evento> Eventos { get; } = [];
        public List<Lugar> Lugares { get; } = [new Lugar { Id = 3, Nombre = "Biblioteca", Estado = "ACTIVO", Validado = true }];
        public Dictionary<int, (byte[] Contenido, string TipoMime, DateTime Actualizada)> Imagenes { get; } = [];
        public HashSet<int> Suspendidos { get; } = [];
        public int VecesModerado { get; private set; }

        public Task<List<Lugar>> ListarLugaresDisponiblesAsync(CancellationToken ct = default)
            => Task.FromResult(Lugares.ToList());

        public Task<Lugar?> ObtenerLugarAsync(int lugarId, CancellationToken ct = default)
            => Task.FromResult(Lugares.FirstOrDefault(l => l.Id == lugarId));

        public Task<List<EventoParaPuente>> ListarPublicadosHoyAsync(
            DateTime desdeUtc,
            DateTime hastaUtc,
            DateTime ahoraUtc,
            CancellationToken ct = default)
            => Task.FromResult(new List<EventoParaPuente>());

        public Task<bool> CrearAsync(
            Evento evento,
            byte[]? imagen,
            string? tipoMime,
            bool aplicarLimiteDiario,
            DateTime desdeUtc,
            DateTime hastaUtc,
            CancellationToken ct = default)
        {
            if (aplicarLimiteDiario && Eventos.Count(e =>
                    e.CreadorId == evento.CreadorId && e.CreatedAt >= desdeUtc && e.CreatedAt < hastaUtc) >= ReglasEvento.MaximoEventosPorDia)
                return Task.FromResult(false);

            evento.Id = Eventos.Count + 1;
            Eventos.Add(evento);
            if (imagen is not null && tipoMime is not null)
                Imagenes[evento.Id] = (imagen, tipoMime, ReglasEvento.VersionImagen(evento.CreatedAt));
            return Task.FromResult(true);
        }

        public Task<List<EventoListado>> ListarMiosAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult(Eventos.Where(e => e.CreadorId == usuarioId).Select(AListado).ToList());

        public Task<Evento?> ObtenerMioAsync(int eventoId, int usuarioId, CancellationToken ct = default)
            => Task.FromResult(Eventos.FirstOrDefault(e => e.Id == eventoId && e.CreadorId == usuarioId));

        public Task GuardarCambiosAsync(Evento evento, byte[]? imagen, string? tipoMime, CancellationToken ct = default)
        {
            if (imagen is not null && tipoMime is not null)
                Imagenes[evento.Id] = (imagen, tipoMime, ReglasEvento.VersionImagen(evento.UpdatedAt));
            return Task.CompletedTask;
        }

        public Task<FotoEvento?> ObtenerImagenAsync(int eventoId, CancellationToken ct = default)
        {
            if (!Imagenes.TryGetValue(eventoId, out var imagen)) return Task.FromResult<FotoEvento?>(null);
            var evento = Eventos.Single(e => e.Id == eventoId);
            return Task.FromResult<FotoEvento?>(new FotoEvento(
                imagen.Contenido, imagen.TipoMime, imagen.Actualizada, evento.CreadorId, evento.Estado,
                Suspendidos.Contains(evento.CreadorId)));
        }

        public Task<List<EventoListado>> ListarPendientesAsync(DateTime ahoraUtc, CancellationToken ct = default)
            => Task.FromResult(Eventos
                .Where(e => e.Estado == Evento.EstadoPendiente && e.Fin > ahoraUtc)
                .Select(AListado).ToList());

        public Task<ResultadoModeracionEvento> ModerarAsync(
            int eventoId,
            string estado,
            string? motivo,
            DateTime ahoraUtc,
            CancellationToken ct = default)
        {
            var evento = Eventos.FirstOrDefault(e => e.Id == eventoId);
            if (evento is null) return Task.FromResult(ResultadoModeracionEvento.NoEncontrado);
            if (evento.Estado != Evento.EstadoPendiente) return Task.FromResult(ResultadoModeracionEvento.YaRevisado);
            if (evento.Fin <= ahoraUtc) return Task.FromResult(ResultadoModeracionEvento.Finalizado);
            evento.Estado = estado;
            evento.Motivo = motivo;
            evento.UpdatedAt = ahoraUtc;
            VecesModerado++;
            return Task.FromResult(ResultadoModeracionEvento.Actualizado);
        }

        private EventoListado AListado(Evento evento)
        {
            var lugar = evento.LugarId is int id
                ? Lugares.First(l => l.Id == id).Nombre
                : evento.LugarTexto!;
            var foto = Imagenes.GetValueOrDefault(evento.Id);
            return new EventoListado(evento, lugar, foto.Actualizada == default ? null : foto.Actualizada);
        }
    }

    private static EventoFormularioRequest Solicitud(
        string titulo = "Feria de ciencias",
        int? lugarId = 3,
        string? lugarTexto = "Biblioteca",
        DateTimeOffset? inicio = null,
        DateTimeOffset? fin = null,
        string? descripcion = null,
        string asistencia = ReglasEvento.AsistenciaFija,
        int? visitaMin = null,
        int? visitaMax = null,
        string? origen = null)
        => new(titulo, descripcion,
            inicio ?? new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.FromHours(-4)),
            fin ?? new DateTimeOffset(2026, 11, 17, 14, 0, 0, TimeSpan.FromHours(-4)),
            lugarId, lugarTexto, asistencia, visitaMin, visitaMax, origen);

    private static (EventoService Servicio, RepositorioFalso Repo) CrearServicio()
    {
        var repo = new RepositorioFalso();
        return (new EventoService(repo, new RelojFijo(Ahora)), repo);
    }

    [Fact]
    public async Task Evento_valido_se_publica_y_guarda_solo_el_id_del_lugar()
    {
        var (servicio, repo) = CrearServicio();

        var dto = await servicio.CrearAsync(Usuario, false, Solicitud(), null);

        var evento = Assert.Single(repo.Eventos);
        Assert.Equal(Evento.EstadoPublicado, evento.Estado);
        Assert.Equal(Evento.OrigenFormulario, evento.Origen);
        Assert.Equal(3, evento.LugarId);
        Assert.Null(evento.LugarTexto);
        Assert.Equal("Biblioteca", dto.Lugar);
    }

    [Fact]
    public async Task Un_lugar_fuera_del_catalogo_se_guarda_como_texto_y_queda_pendiente()
    {
        var (servicio, repo) = CrearServicio();

        await servicio.CrearAsync(Usuario, false, Solicitud(lugarId: null, lugarTexto: "Patio lateral"), null);

        var evento = Assert.Single(repo.Eventos);
        Assert.Null(evento.LugarId);
        Assert.Equal("Patio lateral", evento.LugarTexto);
        Assert.Equal(Evento.EstadoPendiente, evento.Estado);
        Assert.Contains(ReglasEvento.MensajeLugarPendiente, evento.Motivo);
    }

    [Fact]
    public async Task Texto_detectado_por_las_reglas_pasa_a_revision_sin_rechazar_la_solicitud()
    {
        var (servicio, repo) = CrearServicio();

        var dto = await servicio.CrearAsync(Usuario, false, Solicitud(titulo: "Feria para tomar cerveza"), null);

        Assert.Equal(Evento.EstadoPendiente, dto.Estado);
        Assert.Contains(ReglasEvento.MensajeContenidoPendiente, dto.Motivo);
        Assert.Single(repo.Eventos);
    }

    [Fact]
    public async Task El_limite_cuenta_eventos_cancelados_y_se_aplica_al_dia_local()
    {
        var (servicio, repo) = CrearServicio();
        for (var i = 0; i < ReglasEvento.MaximoEventosPorDia; i++)
        {
            repo.Eventos.Add(new Evento
            {
                Id = i + 1,
                CreadorId = Usuario,
                Estado = Evento.EstadoCancelado,
                CreatedAt = Ahora.UtcDateTime
            });
        }

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.CrearAsync(Usuario, false, Solicitud(), null));

        Assert.Equal("LIMITE_DIARIO", error.Codigo);
        Assert.Equal(3, repo.Eventos.Count);
    }

    [Fact]
    public async Task Administrador_publica_directamente_y_no_cuenta_en_el_limite()
    {
        var (servicio, repo) = CrearServicio();
        for (var i = 0; i < ReglasEvento.MaximoEventosPorDia; i++)
            repo.Eventos.Add(new Evento { Id = i + 1, CreadorId = Usuario, CreatedAt = Ahora.UtcDateTime });
        var solicitud = Solicitud(
            titulo: "Evento especial",
            lugarId: null,
            lugarTexto: "Fuera del campus",
            inicio: new DateTimeOffset(2026, 11, 17, 23, 0, 0, TimeSpan.FromHours(-4)),
            fin: new DateTimeOffset(2026, 11, 17, 23, 30, 0, TimeSpan.FromHours(-4)));

        var dto = await servicio.CrearAsync(Usuario, true, solicitud, null);

        Assert.Equal(Evento.EstadoPublicado, dto.Estado);
        Assert.Equal(Evento.OrigenAdmin, dto.Origen);
        Assert.Equal(4, repo.Eventos.Count);
    }

    [Fact]
    public async Task Un_evento_creado_desde_un_flyer_se_guarda_con_origen_FLYER_y_pasa_las_mismas_reglas()
    {
        var (servicio, repo) = CrearServicio();

        var dto = await servicio.CrearAsync(Usuario, false, Solicitud(origen: "FLYER"), null);

        Assert.Equal(Evento.OrigenFlyer, dto.Origen);
        Assert.Equal(Evento.OrigenFlyer, Assert.Single(repo.Eventos).Origen);
        Assert.Equal(Evento.EstadoPublicado, dto.Estado);

        var (otro, repoOtro) = CrearServicio();
        var pendiente = await otro.CrearAsync(
            Usuario, false, Solicitud(lugarId: null, lugarTexto: "Patio lateral", origen: "FLYER"), null);
        Assert.Equal(Evento.EstadoPendiente, pendiente.Estado);
        Assert.Equal(Evento.OrigenFlyer, Assert.Single(repoOtro.Eventos).Origen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FORMULARIO")]
    public void Sin_origen_o_con_FORMULARIO_el_evento_se_guarda_como_FORMULARIO(string? origen)
    {
        var (servicio, repo) = CrearServicio();

        servicio.CrearAsync(Usuario, false, Solicitud(origen: origen), null).GetAwaiter().GetResult();

        Assert.Equal(Evento.OrigenFormulario, Assert.Single(repo.Eventos).Origen);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("INSTAGRAM")]
    public async Task Un_origen_distinto_de_FORMULARIO_o_FLYER_se_rechaza_y_no_se_persiste(string origen)
    {
        var (servicio, repo) = CrearServicio();

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.CrearAsync(Usuario, false, Solicitud(origen: origen), null));

        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal(ReglasEvento.MensajeOrigenInvalido, error.Message);
        Assert.Empty(repo.Eventos);
    }

    [Fact]
    public async Task El_administrador_conserva_el_origen_ADMIN_aunque_pida_FLYER()
    {
        var (servicio, _) = CrearServicio();

        var dto = await servicio.CrearAsync(Usuario, true, Solicitud(origen: "FLYER"), null);

        Assert.Equal(Evento.OrigenAdmin, dto.Origen);
    }

    [Fact]
    public async Task Editar_un_evento_no_cambia_su_origen()
    {
        var (servicio, repo) = CrearServicio();
        var creado = await servicio.CrearAsync(Usuario, false, Solicitud(origen: "FLYER"), null);

        await servicio.ActualizarAsync(creado.Id, Usuario, Solicitud(titulo: "Otro título", origen: "FORMULARIO"), null);

        Assert.Equal(Evento.OrigenFlyer, Assert.Single(repo.Eventos).Origen);
    }

    [Fact]
    public async Task Campos_obligatorios_invalidos_no_se_persisten()
    {
        var (servicio, repo) = CrearServicio();

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.CrearAsync(Usuario, false, Solicitud(titulo: " "), null));

        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Empty(repo.Eventos);
    }

    [Fact]
    public async Task Usuario_solo_puede_cancelar_sus_eventos_y_no_luego_del_inicio()
    {
        var (servicio, repo) = CrearServicio();
        var creada = await servicio.CrearAsync(Usuario, false, Solicitud(), null);

        var ajeno = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CancelarAsync(creada.Id, 99));
        var cancelada = await servicio.CancelarAsync(creada.Id, Usuario);

        Assert.Equal("NO_ENCONTRADO", ajeno.Codigo);
        Assert.Equal(Evento.EstadoCancelado, cancelada.Estado);

        var iniciada = Assert.Single(repo.Eventos);
        iniciada.Inicio = Ahora.UtcDateTime.AddMinutes(-1);
        var tarde = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CancelarAsync(creada.Id, Usuario));
        Assert.Equal("EVENTO_INICIADO", tarde.Codigo);
    }

    [Theory]
    [InlineData(Evento.EstadoRechazado)]
    [InlineData(Evento.EstadoCancelado)]
    public async Task Un_evento_rechazado_o_cancelado_no_se_puede_editar(string estado)
    {
        var (servicio, repo) = CrearServicio();
        repo.Eventos.Add(new Evento
        {
            Id = 1,
            CreadorId = Usuario,
            Titulo = "Original",
            Estado = estado,
            LugarId = 3,
            CreatedAt = Ahora.UtcDateTime,
            UpdatedAt = Ahora.UtcDateTime,
            Inicio = Ahora.UtcDateTime.AddDays(1),
            Fin = Ahora.UtcDateTime.AddDays(1).AddHours(1)
        });

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ActualizarAsync(1, Usuario, Solicitud(titulo: "Cambiado"), null));

        Assert.Equal("EVENTO_NO_EDITABLE", error.Codigo);
        var evento = Assert.Single(repo.Eventos);
        Assert.Equal("Original", evento.Titulo);
        Assert.Equal(estado, evento.Estado);
    }

    [Theory]
    [InlineData(Evento.EstadoPendiente)]
    [InlineData(Evento.EstadoPublicado)]
    public async Task Un_evento_pendiente_o_publicado_se_puede_editar_y_queda_publicado(string estado)
    {
        var (servicio, repo) = CrearServicio();
        repo.Eventos.Add(new Evento
        {
            Id = 1,
            CreadorId = Usuario,
            Titulo = "Original",
            Estado = estado,
            LugarId = 3,
            CreatedAt = Ahora.UtcDateTime,
            UpdatedAt = Ahora.UtcDateTime,
            Inicio = Ahora.UtcDateTime.AddDays(1),
            Fin = Ahora.UtcDateTime.AddDays(1).AddHours(1)
        });

        var dto = await servicio.ActualizarAsync(1, Usuario, Solicitud(titulo: "Cambiado"), null);

        Assert.Equal("Cambiado", dto.Titulo);
        Assert.Equal(Evento.EstadoPublicado, dto.Estado);
    }

    [Fact]
    public async Task Los_pendientes_vencidos_no_se_listan_ni_se_pueden_aprobar()
    {
        var (servicio, repo) = CrearServicio();
        repo.Eventos.Add(new Evento
        {
            Id = 1,
            CreadorId = Usuario,
            Titulo = "Vencido",
            Estado = Evento.EstadoPendiente,
            LugarId = 3,
            CreatedAt = Ahora.UtcDateTime,
            UpdatedAt = Ahora.UtcDateTime,
            Inicio = Ahora.UtcDateTime.AddHours(-2),
            Fin = Ahora.UtcDateTime.AddHours(-1)
        });
        repo.Eventos.Add(new Evento
        {
            Id = 2,
            CreadorId = Usuario,
            Titulo = "Vigente",
            Estado = Evento.EstadoPendiente,
            LugarId = 3,
            CreatedAt = Ahora.UtcDateTime,
            UpdatedAt = Ahora.UtcDateTime,
            Inicio = Ahora.UtcDateTime.AddHours(1),
            Fin = Ahora.UtcDateTime.AddHours(2)
        });

        var pendientes = await servicio.ListarPendientesAsync();

        Assert.Equal("Vigente", Assert.Single(pendientes).Titulo);
        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ModerarAsync(1, new ResolverEventoRequest(Evento.EstadoPublicado, null)));
        Assert.Equal("EVENTO_FINALIZADO", error.Codigo);
        Assert.Equal(Evento.EstadoPendiente, repo.Eventos.Single(e => e.Id == 1).Estado);
    }

    [Fact]
    public async Task La_foto_de_un_creador_suspendido_se_oculta_a_los_demas_pero_no_al_dueno_ni_al_admin()
    {
        var (servicio, repo) = CrearServicio();
        repo.Eventos.Add(new Evento
        {
            Id = 1,
            CreadorId = Usuario,
            Estado = Evento.EstadoPublicado,
            LugarId = 3,
            CreatedAt = Ahora.UtcDateTime,
            UpdatedAt = Ahora.UtcDateTime,
            Inicio = Ahora.UtcDateTime.AddDays(1),
            Fin = Ahora.UtcDateTime.AddDays(1).AddHours(1)
        });
        repo.Imagenes[1] = ([0xFF, 0xD8, 0xFF], "image/jpeg", Ahora.UtcDateTime);
        repo.Suspendidos.Add(Usuario);

        Assert.NotNull(await servicio.ObtenerImagenAsync(1, Usuario, false));
        Assert.NotNull(await servicio.ObtenerImagenAsync(1, 99, true));
        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ObtenerImagenAsync(1, 99, false));
        Assert.Equal("NO_ENCONTRADO", error.Codigo);
    }

    [Fact]
    public async Task Moderar_notifica_a_traves_del_repositorio_y_no_revisa_dos_veces()
    {
        var (servicio, repo) = CrearServicio();
        var creada = await servicio.CrearAsync(Usuario, false, Solicitud(lugarId: null), null);

        await servicio.ModerarAsync(creada.Id, new ResolverEventoRequest(Evento.EstadoPublicado, null));
        var repetida = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ModerarAsync(creada.Id, new ResolverEventoRequest(Evento.EstadoRechazado, "No cumple")));

        Assert.Equal(1, repo.VecesModerado);
        Assert.Equal("YA_REVISADO", repetida.Codigo);
        Assert.Equal(Evento.EstadoPublicado, Assert.Single(repo.Eventos).Estado);
    }
}
