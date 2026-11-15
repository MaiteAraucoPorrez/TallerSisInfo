using Breaku.Application.Admin;
using Breaku.Application.Horarios;
using Breaku.Application.Reportes;
using Breaku.Application.TiempoReal;
using Breaku.Domain.Rules;

namespace Breaku.Tests;

/// <summary>HU27: qué reportes de comunidades y eventos se aceptan, y cómo queda la respuesta del admin.</summary>
public class ReglasReportesCompletosTests
{
    private const string Texto = "Una descripción bastante larga";

    [Fact]
    public void Una_comunidad_se_reporta_con_su_id()
        => Assert.Null(ReglasReporte.ValidarNuevo("comunidad_inapropiada", null, 31, null, Texto));

    [Fact]
    public void Un_evento_se_reporta_con_su_id()
        => Assert.Null(ReglasReporte.ValidarNuevo("EVENTO_INAPROPIADO", null, null, 8, Texto));

    [Theory]
    [InlineData("COMUNIDAD_INAPROPIADA", null, null, null, ReglasReporte.MensajeFaltaComunidad)]
    [InlineData("EVENTO_INAPROPIADO", null, null, null, ReglasReporte.MensajeFaltaEvento)]
    [InlineData("EVENTO_INAPROPIADO", null, null, 0, ReglasReporte.MensajeFaltaEvento)]
    [InlineData("LUGAR_INCORRECTO", null, 31, null, ReglasReporte.MensajeFaltaLugar)]
    public void Sin_su_objeto_dice_cual_falta(string motivo, int? lugar, int? comunidad, int? evento, string mensaje)
        => Assert.Equal(mensaje, ReglasReporte.ValidarNuevo(motivo, lugar, comunidad, evento, Texto));

    [Theory]
    [InlineData("COMUNIDAD_INAPROPIADA", 4, 31, null)]   // un lugar en un reporte de comunidad
    [InlineData("EVENTO_INAPROPIADO", null, 31, 8)]      // una comunidad en un reporte de evento
    [InlineData("LUGAR_INCORRECTO", 4, null, 8)]          // un evento en un reporte de lugar
    [InlineData("SUGERENCIA", null, null, 8)]             // un evento en una sugerencia
    [InlineData("ERROR_APP", null, 31, null)]             // una comunidad en «algo no funciona»
    public void Un_objeto_que_no_corresponde_al_motivo_es_error_de_validacion(string motivo, int? lugar, int? comunidad, int? evento)
        => Assert.Equal(ReglasReporte.MensajeObjetoQueNoCorresponde, ReglasReporte.ValidarNuevo(motivo, lugar, comunidad, evento, Texto));

    [Fact]
    public void El_tope_de_pendientes_es_5()
    {
        Assert.Equal(5, ReglasReporte.MaximoPendientesPorEstudiante);
        Assert.Contains("5 reportes pendientes", ReglasReporte.MensajeDemasiados);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Gracias, ya lo corregimos.  ", "Gracias, ya lo corregimos.")]
    public void La_respuesta_es_opcional_y_se_guarda_sin_espacios_de_mas(string? entrada, string? esperada)
        => Assert.Equal(esperada, ReglasReporte.LimpiarRespuesta(entrada));

    [Fact]
    public void La_respuesta_tiene_hasta_500_caracteres()
    {
        Assert.Null(ReglasReporte.ValidarRespuesta(new string('a', 500)));
        Assert.Null(ReglasReporte.ValidarRespuesta(null));
        Assert.Equal(ReglasReporte.MensajeRespuestaLarga, ReglasReporte.ValidarRespuesta(new string('a', 501)));
    }

    [Fact]
    public void El_aviso_dice_que_se_resolvio_y_cual_era_el_reporte()
        => Assert.Equal("Tu reporte «Evento inapropiado» fue resuelto.",
            ReglasReporte.MensajeAviso("EVENTO_INAPROPIADO", "RESUELTO", null));

    [Fact]
    public void El_aviso_de_un_descarte_lo_dice_y_trae_la_respuesta()
        => Assert.Equal("Tu reporte «Sugerencia» fue descartado. Respuesta: «Ya existe esa opción en Perfil.»",
            ReglasReporte.MensajeAviso("SUGERENCIA", "DESCARTADO", "Ya existe esa opción en Perfil."));

    [Fact]
    public void Una_respuesta_larga_se_recorta_en_el_aviso_para_que_entre_en_200_caracteres()
    {
        var aviso = ReglasReporte.MensajeAviso("COMUNIDAD_INAPROPIADA", "RESUELTO", "Revisamos la comunidad. " + new string('x', 480));

        Assert.True(aviso.Length <= ReglasReporte.LargoMaximoAviso, $"Mide {aviso.Length}");
        Assert.StartsWith("Tu reporte «Comunidad inapropiada» fue resuelto. Respuesta: «Revisamos la comunidad.", aviso);
        Assert.EndsWith("…»", aviso);
    }

    [Fact]
    public void Los_saltos_de_linea_de_la_respuesta_no_rompen_el_aviso()
        => Assert.DoesNotContain('\n', ReglasReporte.MensajeAviso("ERROR_APP", "RESUELTO", "Primera línea\nSegunda línea"));
}

/// <summary>HU27: enviar reportes de comunidades y eventos, y ver los propios.</summary>
public class ReporteServiceHu27Tests
{
    private const int Usuario = 3;

    private sealed class RepositorioFalso : IReporteRepository
    {
        public HashSet<(string Objeto, int Id)> Existentes { get; } = [("COMUNIDAD", 31), ("EVENTO", 8), ("LUGAR", 4)];
        public HashSet<(string Objeto, int Id)> Pendientes { get; } = [];
        public int TotalPendientes { get; set; }
        public List<(string Motivo, int? Lugar, int? Comunidad, int? Evento)> Creados { get; } = [];
        public int? MiosPedidosPor { get; private set; }

        public Task<List<LugarReportableDto>> ListarLugaresAsync(CancellationToken ct = default) => Task.FromResult(new List<LugarReportableDto>());
        public Task<bool> ObjetoExisteAsync(string objeto, int id, CancellationToken ct = default) => Task.FromResult(Existentes.Contains((objeto, id)));
        public Task<int> ContarPendientesAsync(int usuarioId, CancellationToken ct = default) => Task.FromResult(TotalPendientes);
        public Task<bool> TienePendienteSobreAsync(int usuarioId, string objeto, int id, CancellationToken ct = default)
            => Task.FromResult(Pendientes.Contains((objeto, id)));
        public Task<int> CrearAsync(int usuarioId, string motivo, int? lugarId, int? comunidadId, int? eventoId, string descripcion,
            CancellationToken ct = default)
        {
            Creados.Add((motivo, lugarId, comunidadId, eventoId));
            return Task.FromResult(40);
        }
        public Task<List<MiReporteDto>> ListarMiosAsync(int usuarioId, CancellationToken ct = default)
        {
            MiosPedidosPor = usuarioId;
            return Task.FromResult(new List<MiReporteDto>
            {
                new(40, "EVENTO_INAPROPIADO", "EVENTO", 8, "Feria de clubes", "El afiche tiene insultos.", "PENDIENTE",
                    DateTime.UtcNow, null, null)
            });
        }
    }

    private static (ReporteService Servicio, RepositorioFalso Repo) Crear()
    {
        var repo = new RepositorioFalso();
        return (new ReporteService(repo), repo);
    }

    [Fact]
    public async Task Una_comunidad_que_existe_se_reporta_y_queda_pendiente()
    {
        var (servicio, repo) = Crear();

        var r = await servicio.CrearAsync(Usuario, new NuevoReporteDto("COMUNIDAD_INAPROPIADA", null, "El nombre es ofensivo.", ComunidadId: 31));

        Assert.Equal((40, "PENDIENTE"), (r.Id, r.Estado));
        Assert.Equal(("COMUNIDAD_INAPROPIADA", (int?)null, (int?)31, (int?)null), Assert.Single(repo.Creados));
    }

    [Fact]
    public async Task Un_evento_que_existe_se_reporta_con_su_motivo()
    {
        var (servicio, repo) = Crear();

        await servicio.CrearAsync(Usuario, new NuevoReporteDto("evento_inapropiado", null, "El afiche tiene insultos.", EventoId: 8));

        Assert.Equal(("EVENTO_INAPROPIADO", (int?)null, (int?)null, (int?)8), Assert.Single(repo.Creados));
    }

    [Theory]
    [InlineData("COMUNIDAD_INAPROPIADA", 99, null, ReglasReporte.MensajeComunidadNoExiste)]
    [InlineData("EVENTO_INAPROPIADO", null, 99, ReglasReporte.MensajeEventoNoExiste)]
    public async Task Lo_que_no_existe_responde_no_encontrado(string motivo, int? comunidad, int? evento, string mensaje)
    {
        var (servicio, repo) = Crear();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.CrearAsync(Usuario, new NuevoReporteDto(motivo, null, "Una descripción suficiente", comunidad, evento)));

        Assert.Equal(("NO_ENCONTRADO", mensaje), (ex.Codigo, ex.Message));
        Assert.Empty(repo.Creados);
    }

    [Theory]
    [InlineData("COMUNIDAD_INAPROPIADA", "COMUNIDAD", 31, ReglasReporte.MensajeYaReportadaComunidad)]
    [InlineData("EVENTO_INAPROPIADO", "EVENTO", 8, ReglasReporte.MensajeYaReportadoEvento)]
    public async Task No_se_reporta_dos_veces_lo_mismo_mientras_esta_pendiente(string motivo, string objeto, int id, string mensaje)
    {
        var (servicio, repo) = Crear();
        repo.Pendientes.Add((objeto, id));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearAsync(Usuario,
            new NuevoReporteDto(motivo, null, "Una descripción suficiente",
                objeto == "COMUNIDAD" ? id : null, objeto == "EVENTO" ? id : null)));

        Assert.Equal(("YA_REPORTADO", mensaje), (ex.Codigo, ex.Message));
        Assert.Empty(repo.Creados);
    }

    [Fact]
    public async Task Con_4_pendientes_todavia_se_acepta_uno_mas()
    {
        var (servicio, repo) = Crear();
        repo.TotalPendientes = 4;

        await servicio.CrearAsync(Usuario, new NuevoReporteDto("SUGERENCIA", null, "Una sugerencia bastante útil"));

        Assert.Single(repo.Creados);
    }

    [Fact]
    public async Task Con_5_pendientes_se_rechaza_con_un_mensaje_claro()
    {
        var (servicio, repo) = Crear();
        repo.TotalPendientes = 5;

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.CrearAsync(Usuario, new NuevoReporteDto("EVENTO_INAPROPIADO", null, "El afiche tiene insultos.", EventoId: 8)));

        Assert.Equal(("DEMASIADOS_PENDIENTES", ReglasReporte.MensajeDemasiados), (ex.Codigo, ex.Message));
        Assert.Empty(repo.Creados);
    }

    [Fact]
    public async Task Un_objeto_que_no_corresponde_no_llega_a_la_base()
    {
        var (servicio, repo) = Crear();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.CrearAsync(Usuario, new NuevoReporteDto("COMUNIDAD_INAPROPIADA", 4, "Una descripción suficiente", ComunidadId: 31)));

        Assert.Equal(("VALIDACION", ReglasReporte.MensajeObjetoQueNoCorresponde), (ex.Codigo, ex.Message));
        Assert.Empty(repo.Creados);
    }

    [Fact]
    public async Task Mis_reportes_son_solo_los_del_usuario_que_pide()
    {
        var (servicio, repo) = Crear();

        var mios = await servicio.ListarMiosAsync(Usuario);

        Assert.Equal(Usuario, repo.MiosPedidosPor);
        var r = Assert.Single(mios);
        Assert.Equal(("PENDIENTE", (string?)null, (DateTime?)null), (r.Estado, r.Respuesta, r.RevisadoEn));
    }
}

/// <summary>HU27: el admin resuelve o descarta con una respuesta opcional, y el estudiante recibe el aviso.</summary>
public class ResolverReporteConRespuestaTests
{
    private const int Admin = 2;
    private const int Autor = 7;

    private sealed class RepositorioFalso : IAdminRepository
    {
        public Dictionary<int, string> Reportes { get; } = new() { [1] = "PENDIENTE", [2] = "DESCARTADO" };
        public (int Id, string Estado, string? Respuesta)? Guardado { get; private set; }

        public Task<RevisionReporte?> ResolverReporteAsync(int reporteId, string estado, string? respuesta, int adminId, CancellationToken ct = default)
        {
            if (!Reportes.TryGetValue(reporteId, out var anterior)) return Task.FromResult<RevisionReporte?>(null);
            if (anterior == "PENDIENTE")
            {
                Reportes[reporteId] = estado;
                Guardado = (reporteId, estado, respuesta);
            }
            return Task.FromResult<RevisionReporte?>(new RevisionReporte(anterior, Autor));
        }

        public Task<bool?> CambiarValidacionLugarAsync(int lugarId, bool validado, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ResumenAdminDto> ResumenAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<LugarAdminDto>> ListarLugaresAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> CambiarEstadoLugarAsync(int lugarId, string estado, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<ReporteAdminDto>> ListarReportesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<CategoriaAdminDto>> ListarCategoriasAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<CampusAdminDto>> ListarCampusActivosAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LugarDetalleAdminDto?> ObtenerLugarAsync(int lugarId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> NombreEnUsoAsync(string nombre, int? excluirId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CrearLugarAsync(DatosLugar datos, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> EditarLugarAsync(int lugarId, DatosLugar datos, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> LugarEnUsoAsync(int lugarId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> EliminarLugarAsync(int lugarId, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<TurnoConsultaRag>> ListarConsultasRagAsync(int limite, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MetricasAdminDto> MetricasAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class EnVivoFalso(bool falla = false) : ITiempoReal
    {
        public List<int> Avisados { get; } = [];

        public Task AvisosCambiaronAsync(IReadOnlyCollection<int> usuarioIds, CancellationToken ct = default)
        {
            if (falla) throw new InvalidOperationException("SignalR caído");
            Avisados.AddRange(usuarioIds);
            return Task.CompletedTask;
        }
        public Task ComunidadCreadaAsync(int comunidadId, CancellationToken ct = default) => Task.CompletedTask;
        public Task AvisosDeComunidadAsync(int comunidadId, CancellationToken ct = default) => Task.CompletedTask;
        public Task ComunidadActualizadaAsync(int comunidadId, string estado, int confirmados, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (AdminService Servicio, RepositorioFalso Repo, EnVivoFalso EnVivo) Crear(bool enVivoFalla = false)
    {
        var repo = new RepositorioFalso();
        var enVivo = new EnVivoFalso(enVivoFalla);
        return (new AdminService(repo, enVivo), repo, enVivo);
    }

    [Fact]
    public async Task Guarda_la_respuesta_con_la_decision_y_avisa_al_estudiante_en_vivo()
    {
        var (servicio, repo, enVivo) = Crear();

        await servicio.ResolverReporteAsync(1, new ResolverReporteDto("RESUELTO", "  Gracias, ya lo corregimos.  "), Admin);

        Assert.Equal((1, "RESUELTO", "Gracias, ya lo corregimos."), repo.Guardado);
        Assert.Equal([Autor], enVivo.Avisados);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public async Task Sin_respuesta_tambien_se_resuelve(string? respuesta)
    {
        var (servicio, repo, _) = Crear();

        await servicio.ResolverReporteAsync(1, new ResolverReporteDto("DESCARTADO", respuesta), Admin);

        Assert.Equal((1, "DESCARTADO", (string?)null), repo.Guardado);
    }

    [Fact]
    public async Task Una_respuesta_de_mas_de_500_caracteres_no_llega_a_la_base()
    {
        var (servicio, repo, enVivo) = Crear();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.ResolverReporteAsync(1, new ResolverReporteDto("RESUELTO", new string('a', 501)), Admin));

        Assert.Equal(("VALIDACION", ReglasReporte.MensajeRespuestaLarga), (ex.Codigo, ex.Message));
        Assert.Null(repo.Guardado);
        Assert.Empty(enVivo.Avisados);
    }

    [Fact]
    public async Task Un_reporte_ya_revisado_no_vuelve_a_avisar()
    {
        var (servicio, _, enVivo) = Crear();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.ResolverReporteAsync(2, new ResolverReporteDto("RESUELTO", "Otra vez"), Admin));

        Assert.Equal("YA_REVISADO", ex.Codigo);
        Assert.Empty(enVivo.Avisados);
    }

    [Fact]
    public async Task Si_el_aviso_en_vivo_falla_la_revision_igual_queda_guardada()
    {
        var (servicio, repo, _) = Crear(enVivoFalla: true);

        await servicio.ResolverReporteAsync(1, new ResolverReporteDto("RESUELTO", "Listo"), Admin);

        Assert.Equal((1, "RESUELTO", "Listo"), repo.Guardado);
    }
}
