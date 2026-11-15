using System.Security.Claims;
using Breaku.Application.DTOs;
using Breaku.Application.Eventos;
using Breaku.Application.Horarios;
using Breaku.Domain.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Breaku.Api.Controllers;

public class ExtraerEventoForm
{
    /// <summary>Afiche: JPG, PNG o PDF de hasta 8 MB (PDF de hasta 2 páginas).</summary>
    public IFormFile? Archivo { get; set; }

    /// <summary>Texto pegado del evento (alternativa al archivo; uno solo de los dos).</summary>
    public string? Texto { get; set; }
}

[ApiController]
[Authorize]
[Route("api/eventos")]
[Tags("Eventos")]
[Produces("application/json")]
public class ExtraccionEventoController : ControllerBase
{
    private const long LimiteCuerpo = ExtraccionEventoService.BytesMaximos + 1024 * 1024;

    private readonly ExtraccionEventoService _servicio;
    public ExtraccionEventoController(ExtraccionEventoService servicio) => _servicio = servicio;

    private int UsuarioId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")
                  ?? throw new UnauthorizedAccessException());

    /// <summary>HU24: lee un afiche (foto o PDF) o un texto y devuelve un borrador de evento. No crea el evento.</summary>
    [HttpPost("extraer")]
    [EnableRateLimiting(ReglasRendimiento.PoliticaIa)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteCuerpo)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteCuerpo)]
    [ProducesResponseType(typeof(RespuestaExtraccionEventoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Extraer([FromForm] ExtraerEventoForm form, CancellationToken ct)
    {
        try
        {
            byte[]? bytes = null;
            if (form.Archivo is { } archivo)
            {
                if (archivo.Length > ExtraccionEventoService.BytesMaximos)
                    throw new ReglaNegocioException("VALIDACION", "El archivo no puede superar los 8 MB.");
                using var memoria = new MemoryStream();
                await archivo.CopyToAsync(memoria, ct);
                bytes = memoria.ToArray();
            }
            return Ok(await _servicio.ExtraerAsync(UsuarioId, bytes, form.Texto, ct));
        }
        catch (ReglaNegocioException ex)
        {
            var body = new ErrorNegocioDto(ex.Codigo, ex.Message);
            return ex.Codigo == "LECTOR_NO_DISPONIBLE" ? StatusCode(StatusCodes.Status503ServiceUnavailable, body) : BadRequest(body);
        }
    }
}
