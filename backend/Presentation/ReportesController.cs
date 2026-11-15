using System.Security.Claims;
using Breaku.Application.DTOs;
using Breaku.Application.Horarios;
using Breaku.Application.Reportes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Breaku.Api.Controllers;

/// <summary>
/// Reportes de los estudiantes: desde el perfil (algo no funciona, sugerencias, información de un lugar) y desde una
/// comunidad o un evento. Quedan pendientes y los revisa un administrador en el panel (<c>GET /api/admin/reportes</c>); el
/// estudiante sigue los suyos, con la respuesta del admin, en «Mis reportes» (<c>GET /api/reportes/mios</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/reportes")]
[Tags("Reportes")]
[Produces("application/json")]
public class ReportesController : ControllerBase
{
    private readonly ReporteService _service;
    private readonly ILogger<ReportesController> _logger;

    public ReportesController(ReporteService service, ILogger<ReportesController> logger)
    {
        _service = service;
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

    // El autor sale SIEMPRE del token.
    private int UsuarioId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")
                  ?? throw new UnauthorizedAccessException());

    /// <summary>Lugares activos que se pueden reportar (para el selector del formulario).</summary>
    /// <response code="200">Lugares, por nombre.</response>
    [HttpGet("lugares")]
    [ProducesResponseType(typeof(List<LugarReportableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Lugares(CancellationToken ct) => Ok(await _service.ListarLugaresAsync(ct));

    /// <summary>Mis reportes: solo los del usuario del token, con motivo, fecha, estado y la respuesta del admin.</summary>
    /// <remarks>
    /// Primero los pendientes y después los revisados, del más reciente al más antiguo (hasta 100). Un reporte
    /// <c>PENDIENTE</c> viene sin <c>respuesta</c> ni <c>revisadoEn</c>; uno revisado trae <c>revisadoEn</c> y, si el admin
    /// dejó una, la <c>respuesta</c> completa. <c>objetoTipo</c>/<c>objetoId</c>/<c>objeto</c> dicen a qué lugar, comunidad
    /// o evento apunta (<c>null</c> en «algo no funciona» y sugerencias).
    /// </remarks>
    /// <response code="200">Los reportes del usuario (puede ser una lista vacía).</response>
    /// <response code="401">Falta el token, es inválido o está vencido.</response>
    /// <response code="500">Error inesperado (<c>ERROR_INESPERADO</c>, con el código de seguimiento).</response>
    [HttpGet("mios")]
    [ProducesResponseType(typeof(List<MiReporteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Mios(CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ListarMiosAsync(UsuarioId, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not UnauthorizedAccessException)
        {
            return ErrorInesperado(ex, "Mis reportes");
        }
    }

    /// <summary>Envía un reporte.</summary>
    /// <remarks>
    /// Cada motivo lleva solo su objeto: <c>LUGAR_INCORRECTO</c> con <c>lugarId</c>, <c>COMUNIDAD_INAPROPIADA</c> con
    /// <c>comunidadId</c> y <c>EVENTO_INAPROPIADO</c> con <c>eventoId</c>; <c>ERROR_APP</c> y <c>SUGERENCIA</c>, con ninguno.
    /// Un objeto que no corresponde al motivo es 400. La descripción tiene de 10 a 500 caracteres. Un mismo lugar, comunidad o
    /// evento no se reporta otra vez mientras siga pendiente, y cada estudiante tiene como mucho 5 reportes pendientes.
    ///
    /// Ejemplos de petición:
    /// <code>
    /// { "motivo": "LUGAR_INCORRECTO", "lugarId": 4, "descripcion": "El horario dice que abre a las 7, pero abre a las 9." }
    /// { "motivo": "EVENTO_INAPROPIADO", "eventoId": 8, "descripcion": "El afiche tiene insultos." }
    /// </code>
    /// </remarks>
    /// <response code="201">Reporte recibido (queda <c>PENDIENTE</c>).</response>
    /// <response code="400">Falta un dato, no es válido o no corresponde al motivo (<c>VALIDACION</c>; el mensaje dice cuál).</response>
    /// <response code="401">Falta el token, es inválido o está vencido.</response>
    /// <response code="404">El lugar, la comunidad o el evento no existe (<c>NO_ENCONTRADO</c>).</response>
    /// <response code="409">Ya lo reportaste y sigue pendiente (<c>YA_REPORTADO</c>), o ya tienes 5 pendientes (<c>DEMASIADOS_PENDIENTES</c>).</response>
    /// <response code="500">Error inesperado (<c>ERROR_INESPERADO</c>, con el código de seguimiento); no se guardó nada.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ReporteCreadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Crear(NuevoReporteDto req, CancellationToken ct)
    {
        try
        {
            return StatusCode(StatusCodes.Status201Created, await _service.CrearAsync(UsuarioId, req, ct));
        }
        catch (ReglaNegocioException ex)
        {
            var body = new ErrorNegocioDto(ex.Codigo, ex.Message);
            return ex.Codigo switch
            {
                "NO_ENCONTRADO" => NotFound(body),
                "YA_REPORTADO" or "DEMASIADOS_PENDIENTES" => Conflict(body),
                _ => BadRequest(body)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not UnauthorizedAccessException)
        {
            return ErrorInesperado(ex, "el envío de un reporte");
        }
    }
}
