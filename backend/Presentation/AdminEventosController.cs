using Breaku.Application.DTOs;
using Breaku.Application.Eventos;
using Breaku.Application.Horarios;
using Breaku.Domain.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Breaku.Presentation.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/eventos")]
[Tags("Administración de eventos")]
[Produces("application/json")]
public sealed class AdminEventosController : ControllerBase
{
    private readonly EventoService _service;

    public AdminEventosController(EventoService service) => _service = service;

    [HttpGet("pendientes")]
    [ProducesResponseType(typeof(List<EventoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Pendientes(CancellationToken ct)
        => Ok(await _service.ListarPendientesAsync(ct));

    [HttpPatch("{id:int}/moderacion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorNegocioDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Moderar(int id, ResolverEventoRequest request, CancellationToken ct)
    {
        try
        {
            await _service.ModerarAsync(id, request, ct);
            return NoContent();
        }
        catch (ReglaNegocioException ex)
        {
            var error = new ErrorNegocioDto(ex.Codigo, ex.Message);
            return ex.Codigo switch
            {
                "NO_ENCONTRADO" => NotFound(error),
                "YA_REVISADO" => Conflict(error),
                _ => BadRequest(error)
            };
        }
    }
}
