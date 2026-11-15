using Breaku.Application.Horarios;
using Breaku.Application.TiempoReal;
using Breaku.Domain.Rules;

namespace Breaku.Application.Admin;

public class AdminService
{
    public static readonly string[] EstadosLugar = ["ACTIVO", "EN_REVISION", "INACTIVO"];
    public const int LimiteConsultasPorDefecto = 50;
    public const int LimiteConsultasMaximo = 200;

    private readonly IAdminRepository _repo;
    private readonly ITiempoReal _tiempoReal;
    private readonly IEtiquetaRepository? _etiquetas;

    public AdminService(IAdminRepository repo, ITiempoReal? tiempoReal = null, IEtiquetaRepository? etiquetas = null)
    {
        _repo = repo;
        _tiempoReal = tiempoReal ?? SinTiempoReal.Instancia;
        _etiquetas = etiquetas;
    }

    // ---------- HU30: catálogo de etiquetas de tipo de actividad ----------

    public Task<List<EtiquetaDto>> ListarEtiquetasAsync(CancellationToken ct = default) => Etiquetas.ListarAsync(ct);

    public async Task<EtiquetaDto> CrearEtiquetaAsync(NuevaEtiquetaDto req, CancellationToken ct = default)
    {
        var nombre = ReglasEtiquetas.LimpiarNombre(req.Nombre);
        if (ReglasEtiquetas.ValidarNombre(nombre) is { } error) throw new ReglaNegocioException("VALIDACION", error);
        if (await Etiquetas.NombreEnUsoAsync(nombre, ct))
            throw new ReglaNegocioException("NOMBRE_REPETIDO", ReglasEtiquetas.MensajeRepetida);
        return new EtiquetaDto(await Etiquetas.CrearAsync(nombre, ct), nombre);
    }

    private IEtiquetaRepository Etiquetas => _etiquetas ?? throw new InvalidOperationException("Falta IEtiquetaRepository.");

    public Task<ResumenAdminDto> ResumenAsync(CancellationToken ct = default) => _repo.ResumenAsync(ct);

    public Task<List<LugarAdminDto>> ListarLugaresAsync(CancellationToken ct = default) => _repo.ListarLugaresAsync(ct);

    public async Task CambiarEstadoLugarAsync(int lugarId, CambioEstadoLugarDto req, int adminId, CancellationToken ct = default)
    {
        var estado = req.Estado?.Trim().ToUpperInvariant() ?? "";
        if (!EstadosLugar.Contains(estado))
            throw new ReglaNegocioException("VALIDACION", "El estado tiene que ser ACTIVO, EN_REVISION o INACTIVO.");

        // HU30: un lugar recomendable no se activa sin al menos una etiqueta del catálogo.
        if (estado == "ACTIVO" && _etiquetas is not null
            && await _etiquetas.EstadoDelLugarAsync(lugarId, ct) is { } actual
            && !ReglasEtiquetas.PuedeActivarse(actual.Recomendable, actual.Etiquetas))
            throw new ReglaNegocioException("VALIDACION", ReglasEtiquetas.MensajeSinEtiqueta);

        var anterior = await _repo.CambiarEstadoLugarAsync(lugarId, estado, adminId, ct);
        if (anterior is null) throw new ReglaNegocioException("NO_ENCONTRADO", "El lugar no existe.");
    }

    public async Task CambiarValidacionLugarAsync(int lugarId, CambioValidacionLugarDto req, int adminId, CancellationToken ct = default)
    {
        if (req.Validado is not bool validado)
            throw new ReglaNegocioException("VALIDACION", "Indica si el lugar está validado.");

        var anterior = await _repo.CambiarValidacionLugarAsync(lugarId, validado, adminId, ct);
        if (anterior is null) throw new ReglaNegocioException("NO_ENCONTRADO", "El lugar no existe.");
    }

    public Task<List<CategoriaAdminDto>> ListarCategoriasAsync(CancellationToken ct = default) => _repo.ListarCategoriasAsync(ct);

    public Task<List<CampusAdminDto>> ListarCampusAsync(CancellationToken ct = default) => _repo.ListarCampusActivosAsync(ct);

    public async Task<LugarDetalleAdminDto> ObtenerLugarAsync(int lugarId, CancellationToken ct = default)
        => await _repo.ObtenerLugarAsync(lugarId, ct) ?? throw new ReglaNegocioException("NO_ENCONTRADO", "El lugar no existe.");

    /// <summary>Crea un lugar en el catálogo: queda ACTIVO y validado (lo cargó el admin) y se puede recomendar.</summary>
    public async Task<LugarCreadoDto> CrearLugarAsync(GuardarLugarDto req, int adminId, CancellationToken ct = default)
    {
        // Se crea ACTIVO y recomendable: HU30 pide al menos una etiqueta.
        var datos = await ValidarLugarAsync(req, null, quedaActivoYRecomendable: true, ct);
        return new LugarCreadoDto(await _repo.CrearLugarAsync(datos, adminId, ct));
    }

    public async Task EditarLugarAsync(int lugarId, GuardarLugarDto req, int adminId, CancellationToken ct = default)
    {
        var actual = _etiquetas is null ? null : await _etiquetas.EstadoDelLugarAsync(lugarId, ct);
        var datos = await ValidarLugarAsync(req, lugarId, actual is { Estado: "ACTIVO", Recomendable: true }, ct);
        if (!await _repo.EditarLugarAsync(lugarId, datos, adminId, ct))
            throw new ReglaNegocioException("NO_ENCONTRADO", "El lugar no existe.");
    }

    /// <summary>
    /// Elimina un lugar que nunca se usó (por ejemplo, uno cargado por error). Si ya aparece en recomendaciones, reportes o
    /// comunidades, borrarlo rompería ese historial: se responde EN_USO y se sugiere desactivarlo.
    /// </summary>
    public async Task EliminarLugarAsync(int lugarId, int adminId, CancellationToken ct = default)
    {
        if (await _repo.ObtenerLugarAsync(lugarId, ct) is null) throw new ReglaNegocioException("NO_ENCONTRADO", "El lugar no existe.");
        if (await _repo.LugarEnUsoAsync(lugarId, ct)) throw new ReglaNegocioException("EN_USO", ReglasLugar.MensajeEnUso);
        if (!await _repo.EliminarLugarAsync(lugarId, adminId, ct)) throw new ReglaNegocioException("NO_ENCONTRADO", "El lugar no existe.");
    }

    private async Task<DatosLugar> ValidarLugarAsync(GuardarLugarDto req, int? lugarId, bool quedaActivoYRecomendable, CancellationToken ct)
    {
        var apertura = LeerHora(req.HoraApertura);
        var cierre = LeerHora(req.HoraCierre);
        var datos = new DatosLugar(ReglasLugar.LimpiarNombre(req.Nombre), ReglasLugar.LimpiarDescripcion(req.Descripcion),
            req.CategoriaId, req.Latitud, req.Longitud, req.DuracionEstimadaMin, apertura, cierre, req.MinAPieDesdeBloqueA);

        var campus = (await _repo.ListarCampusActivosAsync(ct))
            .Select(c => new CampusRef(c.Nombre, c.Latitud, c.Longitud, c.RadioMaxM)).ToList();
        var error = ReglasLugar.Validar(datos, campus);
        if (error is not null) throw new ReglaNegocioException("VALIDACION", error);

        if (!(await _repo.ListarCategoriasAsync(ct)).Any(c => c.Id == datos.CategoriaId))
            throw new ReglaNegocioException("VALIDACION", ReglasLugar.MensajeCategoria);
        if (await _repo.NombreEnUsoAsync(datos.Nombre!, lugarId, ct))
            throw new ReglaNegocioException("NOMBRE_REPETIDO", ReglasLugar.MensajeNombreRepetido);

        // HU30: etiquetas del catálogo. Al crear son obligatorias; al editar, si no vienen, el lugar conserva las suyas.
        if (_etiquetas is null) return datos;
        var etiquetaIds = ReglasEtiquetas.Normalizar(req.EtiquetaIds) ?? (lugarId is null ? [] : null);
        if (etiquetaIds is null) return datos;

        var catalogo = (await _etiquetas.ListarAsync(ct)).Select(e => e.Id).ToList();
        if (ReglasEtiquetas.ValidarAsignacion(etiquetaIds, catalogo, quedaActivoYRecomendable) is { } errorEtiquetas)
            throw new ReglaNegocioException("VALIDACION", errorEtiquetas);
        return datos with { EtiquetaIds = etiquetaIds };

        // Una hora que vino pero no se puede leer cuenta como horario inválido (no como «sin horario»).
        TimeOnly? LeerHora(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            return ReglasExtraccion.LeerHora(texto) ?? throw new ReglaNegocioException("VALIDACION", ReglasLugar.MensajeHorario);
        }
    }

    public Task<List<ReporteAdminDto>> ListarReportesAsync(CancellationToken ct = default) => _repo.ListarReportesAsync(ct);

    public async Task ResolverReporteAsync(int reporteId, ResolverReporteDto req, int adminId, CancellationToken ct = default)
    {
        var decision = ReglasReporte.LeerDecision(req.Estado)
                       ?? throw new ReglaNegocioException("VALIDACION", ReglasReporte.MensajeDecisionInvalida);

        // HU27: respuesta opcional, sin caracteres de control y de hasta 500 caracteres.
        var respuesta = ReglasReporte.LimpiarRespuesta(req.Respuesta is null ? null : ReglasSeguridadIa.Sanear(req.Respuesta));
        if (ReglasReporte.ValidarRespuesta(respuesta) is { } error) throw new ReglaNegocioException("VALIDACION", error);

        // La decisión, la respuesta y el aviso al estudiante se guardan juntos (una transacción en el repositorio).
        var revision = await _repo.ResolverReporteAsync(reporteId, decision, respuesta, adminId, ct)
                       ?? throw new ReglaNegocioException("NO_ENCONTRADO", ReglasReporte.MensajeNoExiste);
        if (!ReglasReporte.SePuedeRevisar(revision.EstadoAnterior))
            throw new ReglaNegocioException("YA_REVISADO", ReglasReporte.MensajeYaRevisado);

        // La campanita del estudiante se actualiza en vivo; si el canal falla, el aviso ya quedó guardado.
        try { await _tiempoReal.AvisosCambiaronAsync([revision.UsuarioId], ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }

    public async Task<List<ConsultaRagAdminDto>> ListarConsultasRagAsync(int? limite, CancellationToken ct = default)
        => AuditoriaConversaciones.Agrupar(
            await _repo.ListarConsultasRagAsync(Math.Clamp(limite ?? LimiteConsultasPorDefecto, 1, LimiteConsultasMaximo), ct));

    public Task<MetricasAdminDto> MetricasAsync(CancellationToken ct = default) => _repo.MetricasAsync(ct);
}
