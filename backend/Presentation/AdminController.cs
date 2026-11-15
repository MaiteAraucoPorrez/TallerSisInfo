using System.Security.Claims;
using Breaku.Application.Admin;
using Breaku.Application.DTOs;
using Breaku.Application.Horarios;
using Breaku.Application.Lugares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Breaku.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin")]
[Tags("Administración")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private const long LimiteFoto = Breaku.Domain.Rules.ReglasFotoLugar.BytesMaximos + 512 * 1024;

    private readonly AdminService _service;
    private readonly FotoLugarService _fotos;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AdminService service, FotoLugarService fotos, ILogger<AdminController> logger)
    {
        _service = service;
        _fotos = fotos;
        _logger = logger;
    }

    /// <summary>Un error que no esperábamos: se registra con la traza de la solicitud y se responde 500 con ese código.</summary>
    private ObjectResult ErrorInesperado(Exception ex, string operacion)
    {
        var traza = HttpContext.TraceIdentifier;
        _logger.LogError(ex, "Error inesperado en {Operacion} (traza {Traza})", operacion, traza);
        return StatusCode(StatusCodes.Status500InternalServerError,
            new ErrorNegocioDto("ERROR_INESPERADO", $"No pudimos completar la operación. Código de seguimiento: {traza}."));
    }

    private int AdminId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")
                  ?? throw new UnauthorizedAccessException());

    [HttpGet("resumen")]
    [ProducesResponseType(typeof(ResumenAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _service.ResumenAsync(ct));

    [HttpGet("lugares")]
    [ProducesResponseType(typeof(List<LugarAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Lugares(CancellationToken ct) => Ok(await _service.ListarLugaresAsync(ct));

    [HttpPatch("lugares/{id:int}/estado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(int id, CambioEstadoLugarDto req, CancellationToken ct)
    {
        try
        {
            await _service.CambiarEstadoLugarAsync(id, req, AdminId, ct);
            return NoContent();
        }
        catch (ReglaNegocioException ex)
        {
            // HU30: activar un lugar recomendable sin etiquetas es 400 VALIDACION.
            var body = new ErrorNegocioDto(ex.Codigo, ex.Message);
            return ex.Codigo == "NO_ENCONTRADO" ? NotFound(body) : BadRequest(body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not UnauthorizedAccessException)
        {
            return ErrorInesperado(ex, "el cambio de estado de un lugar");
        }
    }

    [HttpPatch("lugares/{id:int}/validacion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarValidacion(int id, CambioValidacionLugarDto req, CancellationToken ct)
    {
        try
        {
            await _service.CambiarValidacionLugarAsync(id, req, AdminId, ct);
            return NoContent();
        }
        catch (ReglaNegocioException ex)
        {
            var body = new ErrorNegocioDto(ex.Codigo, ex.Message);
            return ex.Codigo == "NO_ENCONTRADO" ? NotFound(body) : BadRequest(body);
        }
    }

    /// <summary>Categorías del catálogo (para el formulario de lugar).</summary>
    [HttpGet("categorias")]
    [ProducesResponseType(typeof(List<CategoriaAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Categorias(CancellationToken ct) => Ok(await _service.ListarCategoriasAsync(ct));

    /// <summary>Campus activos con su centro y su radio: un lugar tiene que quedar dentro de alguno.</summary>
    [HttpGet("campus")]
    [ProducesResponseType(typeof(List<CampusAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Campus(CancellationToken ct) => Ok(await _service.ListarCampusAsync(ct));

    /// <summary>HU30: el catálogo de etiquetas de tipo de actividad (para el selector del formulario de lugar).</summary>
    /// <response code="200">Las etiquetas, por nombre.</response>
    /// <response code="500">Error inesperado (<c>ERROR_INESPERADO</c>, con el código de seguimiento).</response>
    [HttpGet("etiquetas")]
    [ProducesResponseType(typeof(List<EtiquetaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> Etiquetas(CancellationToken ct) => Responder(async () => Ok(await _service.ListarEtiquetasAsync(ct)));

    /// <summary>HU30: agrega una etiqueta al catálogo. Solo un administrador.</summary>
    /// <remarks>
    /// Ejemplo de petición:
    /// <code>
    /// { "nombre": "Voluntariado" }
    /// </code>
    /// </remarks>
    /// <response code="201">Etiqueta creada (<c>{ "id": …, "nombre": … }</c>).</response>
    /// <response code="400">Nombre vacío o de más de 50 caracteres (<c>VALIDACION</c>).</response>
    /// <response code="403">El usuario no es administrador.</response>
    /// <response code="409">Ya existe una con ese nombre, sin distinguir mayúsculas ni tildes (<c>NOMBRE_REPETIDO</c>).</response>
    /// <response code="500">Error inesperado (<c>ERROR_INESPERADO</c>, con el código de seguimiento).</response>
    [HttpPost("etiquetas")]
    [ProducesResponseType(typeof(EtiquetaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> CrearEtiqueta(NuevaEtiquetaDto req, CancellationToken ct)
        => Responder(async () => StatusCode(StatusCodes.Status201Created, await _service.CrearEtiquetaAsync(req, ct)));

    /// <summary>Un lugar con todos sus datos editables.</summary>
    /// <response code="200">El lugar.</response>
    /// <response code="404">El lugar no existe (<c>NO_ENCONTRADO</c>).</response>
    [HttpGet("lugares/{id:int}")]
    [ProducesResponseType(typeof(LugarDetalleAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Lugar(int id, CancellationToken ct)
        => Responder(async () => Ok(await _service.ObtenerLugarAsync(id, ct)));

    /// <summary>Crea un lugar en el catálogo. Queda activo y validado, y se puede recomendar.</summary>
    /// <remarks>
    /// El lugar tiene que quedar dentro del radio de un campus activo (<c>GET /api/admin/campus</c>), con un nombre único,
    /// una duración de 1 a 240 minutos y, si tiene horario, apertura y cierre (el cierre después). Queda en el historial.
    /// </remarks>
    /// <response code="201">Lugar creado (<c>{ "id": … }</c>).</response>
    /// <response code="400">Algún dato no es válido (<c>VALIDACION</c>; el mensaje dice cuál).</response>
    /// <response code="409">Ya hay un lugar con ese nombre (<c>NOMBRE_REPETIDO</c>).</response>
    [HttpPost("lugares")]
    [ProducesResponseType(typeof(LugarCreadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status409Conflict)]
    public Task<IActionResult> CrearLugar(GuardarLugarDto req, CancellationToken ct)
        => Responder(async () => StatusCode(StatusCodes.Status201Created, await _service.CrearLugarAsync(req, AdminId, ct)));

    /// <summary>Edita un lugar del catálogo (mismas reglas que al crearlo). Queda el antes y el después en el historial.</summary>
    /// <response code="204">Lugar actualizado.</response>
    /// <response code="400">Algún dato no es válido (<c>VALIDACION</c>).</response>
    /// <response code="404">El lugar no existe (<c>NO_ENCONTRADO</c>).</response>
    /// <response code="409">Ya hay otro lugar con ese nombre (<c>NOMBRE_REPETIDO</c>).</response>
    [HttpPut("lugares/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status409Conflict)]
    public Task<IActionResult> EditarLugar(int id, GuardarLugarDto req, CancellationToken ct)
        => Responder(async () => { await _service.EditarLugarAsync(id, req, AdminId, ct); return NoContent(); });

    /// <summary>Elimina un lugar que nunca se usó.</summary>
    /// <remarks>
    /// Si el lugar ya aparece en recomendaciones, reportes o comunidades, borrarlo rompería ese historial: responde 409
    /// <c>EN_USO</c> y hay que desactivarlo (<c>PATCH /api/admin/lugares/{id}/estado</c>). El borrado queda en el historial.
    /// </remarks>
    /// <response code="204">Lugar eliminado.</response>
    /// <response code="404">El lugar no existe (<c>NO_ENCONTRADO</c>).</response>
    /// <response code="409">El lugar ya se usó (<c>EN_USO</c>).</response>
    [HttpDelete("lugares/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status409Conflict)]
    public Task<IActionResult> EliminarLugar(int id, CancellationToken ct)
        => Responder(async () => { await _service.EliminarLugarAsync(id, AdminId, ct); return NoContent(); });

    /// <summary>Sube o reemplaza la foto real de un lugar.</summary>
    /// <remarks>
    /// <c>multipart/form-data</c> con el archivo en el campo <c>foto</c>: JPG, PNG o WebP de hasta 2 MB (el panel la achica
    /// antes de subirla). El tipo se revisa por el contenido, no por la extensión. Queda en el historial.
    /// </remarks>
    /// <response code="200">Foto guardada; <c>fotoUrl</c> trae la ruta con la versión nueva.</response>
    /// <response code="400">Sin foto, más de 2 MB o no es JPG, PNG ni WebP (<c>VALIDACION</c>).</response>
    /// <response code="404">El lugar no existe (<c>NO_ENCONTRADO</c>).</response>
    [HttpPut("lugares/{id:int}/foto")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteFoto)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteFoto)]
    [ProducesResponseType(typeof(FotoGuardadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    public Task<IActionResult> SubirFoto(int id, IFormFile? foto, CancellationToken ct) => Responder(async () =>
    {
        if (foto is null || foto.Length == 0) throw new ReglaNegocioException("VALIDACION", Breaku.Domain.Rules.ReglasFotoLugar.MensajeVacia);
        if (foto.Length > Breaku.Domain.Rules.ReglasFotoLugar.BytesMaximos)
            throw new ReglaNegocioException("VALIDACION", Breaku.Domain.Rules.ReglasFotoLugar.MensajeGrande);

        using var memoria = new MemoryStream();
        await foto.CopyToAsync(memoria, ct);
        return Ok(await _fotos.SubirAsync(id, memoria.ToArray(), AdminId, ct));
    });

    /// <summary>Quita la foto de un lugar; vuelve a mostrarse la ilustración de su categoría.</summary>
    /// <response code="204">Foto quitada (o el lugar ya no tenía).</response>
    /// <response code="404">El lugar no existe (<c>NO_ENCONTRADO</c>).</response>
    [HttpDelete("lugares/{id:int}/foto")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    public Task<IActionResult> QuitarFoto(int id, CancellationToken ct)
        => Responder(async () => { await _fotos.EliminarAsync(id, AdminId, ct); return NoContent(); });

    private async Task<IActionResult> Responder(Func<Task<IActionResult>> accion)
    {
        try
        {
            return await accion();
        }
        catch (ReglaNegocioException ex)
        {
            var body = new ErrorNegocioDto(ex.Codigo, ex.Message);
            return ex.Codigo switch
            {
                "NO_ENCONTRADO" => NotFound(body),
                "NOMBRE_REPETIDO" or "EN_USO" => Conflict(body),
                _ => BadRequest(body)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not UnauthorizedAccessException)
        {
            return ErrorInesperado(ex, "el catálogo de lugares");
        }
    }

    /// <summary>Bandeja de reportes: primero los pendientes. Cada uno dice a qué lugar, comunidad o evento se refiere.</summary>
    /// <remarks>
    /// <c>objetoTipo</c> es <c>LUGAR</c>, <c>COMUNIDAD</c> o <c>EVENTO</c> (<c>null</c> en «algo no funciona» y sugerencias),
    /// <c>objetoId</c> su id y <c>objeto</c> su nombre. Los revisados traen <c>respuesta</c> (si se dejó) y <c>revisadoEn</c>.
    /// </remarks>
    /// <response code="200">Hasta 100 reportes.</response>
    /// <response code="500">Error inesperado (<c>ERROR_INESPERADO</c>, con el código de seguimiento).</response>
    [HttpGet("reportes")]
    [ProducesResponseType(typeof(List<ReporteAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Reportes(CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ListarReportesAsync(ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInesperado(ex, "la bandeja de reportes");
        }
    }

    /// <summary>Resuelve o descarta un reporte pendiente, con una respuesta opcional para el estudiante.</summary>
    /// <remarks>
    /// La decisión, la respuesta (hasta 500 caracteres) y el aviso <c>REPORTE_RESUELTO</c> para quien lo envió se guardan en
    /// la misma transacción. El aviso lleva la respuesta recortada; completa se lee en «Mis reportes».
    ///
    /// Ejemplo de petición:
    /// <code>
    /// { "estado": "RESUELTO", "respuesta": "Gracias, ya corregimos el horario." }
    /// </code>
    /// </remarks>
    /// <response code="204">Reporte revisado y estudiante avisado.</response>
    /// <response code="400">Estado inválido o respuesta de más de 500 caracteres (<c>VALIDACION</c>).</response>
    /// <response code="404">El reporte no existe (<c>NO_ENCONTRADO</c>).</response>
    /// <response code="409">El reporte ya fue revisado (<c>YA_REVISADO</c>).</response>
    /// <response code="500">Error inesperado (<c>ERROR_INESPERADO</c>, con el código de seguimiento); no se guardó nada.</response>
    [HttpPatch("reportes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResolverReporte(int id, ResolverReporteDto req, CancellationToken ct)
    {
        try
        {
            await _service.ResolverReporteAsync(id, req, AdminId, ct);
            return NoContent();
        }
        catch (ReglaNegocioException ex)
        {
            var body = new ErrorNegocioDto(ex.Codigo, ex.Message);
            return ex.Codigo switch
            {
                "NO_ENCONTRADO" => NotFound(body),
                "YA_REVISADO" => Conflict(body),
                _ => BadRequest(body)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInesperado(ex, "la revisión de un reporte");
        }
    }

    [HttpGet("auditoria-rag")]
    [ProducesResponseType(typeof(List<ConsultaRagAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AuditoriaRag([FromQuery] int? limite, CancellationToken ct)
        => Ok(await _service.ListarConsultasRagAsync(limite, ct));

    [HttpGet("metricas")]
    [ProducesResponseType(typeof(MetricasAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Metricas(CancellationToken ct) => Ok(await _service.MetricasAsync(ct));
}
