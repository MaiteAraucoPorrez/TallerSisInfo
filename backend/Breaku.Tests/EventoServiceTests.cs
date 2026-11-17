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

        public Task GuardarCambiosAsync(
            Evento evento, byte[]? imagen, string? tipoMime, bool quitarImagen = false, CancellationToken ct = default)
        {
            if (imagen is not null && tipoMime is not null)
                Imagenes[evento.Id] = (imagen, tipoMime, ReglasEvento.VersionImagen(evento.UpdatedAt));
            else if (quitarImagen)
                Imagenes.Remove(evento.Id);
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

        public Task<List<EventoListado>> ListarPublicadosAsync(DateTime ahoraUtc, DateTime hastaUtc, CancellationToken ct = default)
            => Task.FromResult(Eventos
                .Where(e => e.Estado == Evento.EstadoPublicado
                    && e.Fin > ahoraUtc
                    && e.Inicio < hastaUtc
                    && !Suspendidos.Contains(e.CreadorId))
                .Select(AListado).ToList());

        public Task<EventoListado?> ObtenerPublicadoAsync(int eventoId, DateTime ahoraUtc, CancellationToken ct = default)
            => Task.FromResult(Eventos
                .Where(e => e.Id == eventoId && e.Estado == Evento.EstadoPublicado && !Suspendidos.Contains(e.CreadorId))
                .Select(AListado).FirstOrDefault());

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

    private static readonly byte[] FotoJpg = [0xFF, 0xD8, 0xFF, 0x01];
    private static readonly byte[] FotoPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x02];

    private static async Task<(EventoService Servicio, RepositorioFalso Repo, int Id)> EventoConFotoAsync()
    {
        var (servicio, repo) = CrearServicio();
        var creado = await servicio.CrearAsync(Usuario, false, Solicitud(), FotoJpg);
        return (servicio, repo, creado.Id);
    }

    [Fact]
    public async Task Editar_con_una_foto_nueva_reemplaza_la_anterior()
    {
        var (servicio, repo, id) = await EventoConFotoAsync();
        repo.Imagenes[id] = (FotoJpg, "image/jpeg", Ahora.UtcDateTime.AddDays(-1)); // la foto se subió ayer
        var antes = (await servicio.ListarMiosAsync(Usuario)).Single().FotoUrl;

        var dto = await servicio.ActualizarAsync(id, Usuario, Solicitud(titulo: "Con foto nueva"), FotoPng);

        var imagen = Assert.Single(repo.Imagenes).Value;
        Assert.Equal(FotoPng, imagen.Contenido);
        Assert.Equal("image/png", imagen.TipoMime);
        Assert.NotNull(dto.FotoUrl);
        Assert.NotEqual(antes, dto.FotoUrl);
    }

    [Fact]
    public async Task Editar_sin_tocar_la_foto_la_conserva()
    {
        var (servicio, repo, id) = await EventoConFotoAsync();

        var dto = await servicio.ActualizarAsync(id, Usuario, Solicitud(titulo: "Solo cambia el título"), null);

        Assert.Equal(FotoJpg, Assert.Single(repo.Imagenes).Value.Contenido);
        Assert.NotNull(dto.FotoUrl);
    }

    [Fact]
    public async Task Quitar_la_foto_al_editar_la_borra_y_el_evento_queda_sin_fotoUrl()
    {
        var (servicio, repo, id) = await EventoConFotoAsync();
        var solicitud = Solicitud(titulo: "Sin foto") with { QuitarFoto = true };

        var dto = await servicio.ActualizarAsync(id, Usuario, solicitud, null);

        Assert.Empty(repo.Imagenes);
        Assert.Null(dto.FotoUrl);
        Assert.Equal("Sin foto", dto.Titulo);
        Assert.Null((await servicio.ListarMiosAsync(Usuario)).Single().FotoUrl);
    }

    [Fact]
    public async Task Quitar_la_foto_de_un_evento_sin_foto_no_falla()
    {
        var (servicio, repo) = CrearServicio();
        var creado = await servicio.CrearAsync(Usuario, false, Solicitud(), null);

        var dto = await servicio.ActualizarAsync(creado.Id, Usuario, Solicitud() with { QuitarFoto = true }, null);

        Assert.Empty(repo.Imagenes);
        Assert.Null(dto.FotoUrl);
    }

    [Fact]
    public async Task No_se_puede_subir_una_foto_nueva_y_quitar_la_actual_a_la_vez()
    {
        var (servicio, repo, id) = await EventoConFotoAsync();

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ActualizarAsync(id, Usuario, Solicitud(titulo: "Cambiado") with { QuitarFoto = true }, FotoPng));

        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal(ReglasEvento.MensajeFotoNuevaYQuitar, error.Message);
        Assert.Equal(FotoJpg, Assert.Single(repo.Imagenes).Value.Contenido);
        Assert.Equal("Feria de ciencias", repo.Eventos.Single().Titulo);
    }

    [Fact]
    public async Task Solo_el_dueno_puede_cambiar_o_quitar_la_foto()
    {
        var (servicio, repo, id) = await EventoConFotoAsync();

        var alQuitar = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ActualizarAsync(id, 99, Solicitud() with { QuitarFoto = true }, null));
        var alCambiar = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ActualizarAsync(id, 99, Solicitud(), FotoPng));

        Assert.Equal("NO_ENCONTRADO", alQuitar.Codigo);
        Assert.Equal("NO_ENCONTRADO", alCambiar.Codigo);
        Assert.Equal(FotoJpg, Assert.Single(repo.Imagenes).Value.Contenido);
    }

    [Theory]
    [InlineData(Evento.EstadoRechazado)]
    [InlineData(Evento.EstadoCancelado)]
    public async Task En_un_evento_no_editable_no_se_toca_la_foto(string estado)
    {
        var (servicio, repo, id) = await EventoConFotoAsync();
        repo.Eventos.Single().Estado = estado;

        var alQuitar = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ActualizarAsync(id, Usuario, Solicitud() with { QuitarFoto = true }, null));
        var alCambiar = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ActualizarAsync(id, Usuario, Solicitud(), FotoPng));

        Assert.Equal("EVENTO_NO_EDITABLE", alQuitar.Codigo);
        Assert.Equal("EVENTO_NO_EDITABLE", alCambiar.Codigo);
        Assert.Equal(FotoJpg, Assert.Single(repo.Imagenes).Value.Contenido);
    }

    [Fact]
    public async Task Un_evento_que_ya_empezo_no_permite_cambiar_ni_quitar_la_foto()
    {
        var (servicio, repo, id) = await EventoConFotoAsync();
        repo.Eventos.Single().Inicio = Ahora.UtcDateTime.AddMinutes(-5);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.ActualizarAsync(id, Usuario, Solicitud() with { QuitarFoto = true }, null));

        Assert.Equal("EVENTO_INICIADO", error.Codigo);
        Assert.Single(repo.Imagenes);
    }

    [Fact]
    public async Task Una_foto_nueva_invalida_se_rechaza_y_la_anterior_se_conserva()
    {
        var (servicio, repo, id) = await EventoConFotoAsync();
        var noEsImagen = new byte[] { 1, 2, 3, 4 };
        var demasiadoGrande = new byte[ReglasEvento.BytesMaximosImagen + 1];
        demasiadoGrande[0] = 0xFF; demasiadoGrande[1] = 0xD8; demasiadoGrande[2] = 0xFF;

        foreach (var invalida in new[] { noEsImagen, demasiadoGrande, Array.Empty<byte>() })
        {
            var error = await Assert.ThrowsAsync<ReglaNegocioException>(
                () => servicio.ActualizarAsync(id, Usuario, Solicitud(titulo: "No debe cambiar"), invalida));
            Assert.Equal("VALIDACION", error.Codigo);
        }

        Assert.Equal(FotoJpg, Assert.Single(repo.Imagenes).Value.Contenido);
        Assert.Equal("Feria de ciencias", repo.Eventos.Single().Titulo);
    }

    [Fact]
    public async Task Cambiar_la_foto_no_altera_el_refiltro_del_texto_al_editar()
    {
        var (servicio, _, id) = await EventoConFotoAsync();

        var dto = await servicio.ActualizarAsync(
            id, Usuario, Solicitud(lugarId: null, lugarTexto: "Patio lateral"), FotoPng);

        Assert.Equal(Evento.EstadoPendiente, dto.Estado);
        Assert.NotNull(dto.FotoUrl);
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

    // ── Campanita del admin (migración 030) ──

    private static (EventoService Servicio, RepositorioFalso Repo, AvisosAdminTests.AvisosFalsos Avisos) ConAvisos()
    {
        var repo = new RepositorioFalso();
        var avisos = new AvisosAdminTests.AvisosFalsos();
        return (new EventoService(repo, new RelojFijo(Ahora), avisos), repo, avisos);
    }

    [Fact]
    public async Task Un_evento_que_queda_pendiente_avisa_a_los_admins()
    {
        var (servicio, _, avisos) = ConAvisos();
        await servicio.CrearAsync(Usuario, false, Solicitud(lugarId: null, lugarTexto: "Patio lateral"), null);
        Assert.Equal(["evento-por-revisar:1"], avisos.Llamadas);
    }

    [Fact]
    public async Task Un_evento_publicado_no_queda_como_pendiente_para_el_admin()
    {
        var (servicio, _, avisos) = ConAvisos();
        await servicio.CrearAsync(Usuario, false, Solicitud(), null);
        Assert.DoesNotContain(avisos.Llamadas, l => l.StartsWith("evento-por-revisar"));
    }

    [Fact]
    public async Task Moderar_o_cancelar_cierra_el_aviso_del_admin()
    {
        var (servicio, _, avisos) = ConAvisos();
        var creado = await servicio.CrearAsync(Usuario, false, Solicitud(lugarId: null, lugarTexto: "Patio lateral"), null);
        await servicio.ModerarAsync(creado.Id, new ResolverEventoRequest(Evento.EstadoPublicado, null));
        Assert.Equal(["evento-por-revisar:1", "evento-revisado:1"], avisos.Llamadas);

        var otro = await servicio.CrearAsync(Usuario, false, Solicitud(lugarId: null, lugarTexto: "Patio trasero"), null);
        await servicio.CancelarAsync(otro.Id, Usuario);
        Assert.Equal("evento-revisado:2", avisos.Llamadas[^1]);
    }

    private static Evento EventoVisible(
        int id, DateTime inicio, DateTime fin, int creador = Usuario, int? lugarId = 3,
        string? lugarTexto = null, string estado = Evento.EstadoPublicado)
        => new()
        {
            Id = id,
            CreadorId = creador,
            Titulo = $"Evento {id}",
            Estado = estado,
            LugarId = lugarId,
            LugarTexto = lugarTexto,
            Asistencia = ReglasEvento.AsistenciaFija,
            Inicio = inicio,
            Fin = fin,
            CreatedAt = Ahora.UtcDateTime,
            UpdatedAt = Ahora.UtcDateTime
        };

    [Fact]
    public async Task Hoy_y_esta_semana_solo_listan_publicados_que_no_terminaron_y_ordenados_por_inicio()
    {
        var (servicio, repo) = CrearServicio();
        var ahora = Ahora.UtcDateTime;
        repo.Eventos.Add(EventoVisible(1, ahora.AddHours(2), ahora.AddHours(3)));
        repo.Eventos.Add(EventoVisible(2, ahora.AddHours(-3), ahora.AddHours(-1)));
        repo.Eventos.Add(EventoVisible(3, ahora.AddHours(1), ahora.AddHours(2), estado: Evento.EstadoPendiente));
        repo.Eventos.Add(EventoVisible(4, ahora.AddDays(1), ahora.AddDays(1).AddHours(1)));
        repo.Eventos.Add(EventoVisible(5, ahora.AddHours(1), ahora.AddHours(2), creador: 99));
        repo.Eventos.Add(EventoVisible(6, ahora.AddDays(8), ahora.AddDays(8).AddHours(1)));
        repo.Suspendidos.Add(99);

        var hoy = await servicio.ListarPublicadosAsync("hoy");
        var semana = await servicio.ListarPublicadosAsync("Semana");

        Assert.Equal([1], hoy.Select(e => e.Id));
        Assert.Equal([1, 4], semana.Select(e => e.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mes")]
    public async Task Un_rango_distinto_de_hoy_y_semana_da_error_de_validacion(string? rango)
    {
        var (servicio, _) = CrearServicio();

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ListarPublicadosAsync(rango));

        Assert.Equal("VALIDACION", error.Codigo);
    }

    [Fact]
    public async Task La_tarjeta_usa_el_lugar_del_catalogo_o_el_texto_marcado_como_fuera_del_campus()
    {
        var (servicio, repo) = CrearServicio();
        var ahora = Ahora.UtcDateTime;
        repo.Eventos.Add(EventoVisible(1, ahora.AddHours(1), ahora.AddHours(2)));
        repo.Eventos.Add(EventoVisible(2, ahora.AddHours(2), ahora.AddHours(3), lugarId: null, lugarTexto: "Plaza 14 de Septiembre"));

        var lista = await servicio.ListarPublicadosAsync("hoy");

        var catalogo = lista.Single(e => e.Id == 1);
        Assert.Equal("Biblioteca", catalogo.Lugar);
        Assert.False(catalogo.FueraDelCampus);
        var fuera = lista.Single(e => e.Id == 2);
        Assert.Equal("Plaza 14 de Septiembre", fuera.Lugar);
        Assert.True(fuera.FueraDelCampus);
    }

    [Fact]
    public async Task El_detalle_solo_existe_para_eventos_publicados()
    {
        var (servicio, repo) = CrearServicio();
        var ahora = Ahora.UtcDateTime;
        repo.Eventos.Add(EventoVisible(1, ahora.AddHours(1), ahora.AddHours(2)));
        repo.Eventos.Add(EventoVisible(2, ahora.AddHours(1), ahora.AddHours(2), estado: Evento.EstadoPendiente));

        Assert.Equal("Evento 1", (await servicio.ObtenerPublicadoAsync(1)).Titulo);
        foreach (var id in new[] { 2, 99 })
        {
            var error = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.ObtenerPublicadoAsync(id));
            Assert.Equal("NO_ENCONTRADO", error.Codigo);
        }
    }
}
