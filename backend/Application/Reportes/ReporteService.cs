using Breaku.Application.Horarios;
using Breaku.Domain.Rules;

namespace Breaku.Application.Reportes;

/// <summary>
/// Reportes de los estudiantes: «algo no funciona», sugerencias, información incorrecta de un lugar, comunidades y eventos
/// inapropiados. Quedan PENDIENTE y los revisa un administrador en el panel. Para que nadie llene la bandeja del admin: un
/// mismo lugar, comunidad o evento no se reporta dos veces mientras siga pendiente, y cada estudiante tiene como mucho 5
/// reportes pendientes a la vez. En «Mis reportes» el estudiante ve cada uno con su estado y la respuesta del admin.
/// </summary>
public class ReporteService
{
    private readonly IReporteRepository _repo;
    public ReporteService(IReporteRepository repo) => _repo = repo;

    public Task<List<LugarReportableDto>> ListarLugaresAsync(CancellationToken ct = default) => _repo.ListarLugaresAsync(ct);

    public Task<List<MiReporteDto>> ListarMiosAsync(int usuarioId, CancellationToken ct = default) => _repo.ListarMiosAsync(usuarioId, ct);

    public async Task<ReporteCreadoDto> CrearAsync(int usuarioId, NuevoReporteDto req, CancellationToken ct = default)
    {
        var error = ReglasReporte.ValidarNuevo(req.Motivo, req.LugarId, req.ComunidadId, req.EventoId, req.Descripcion);
        if (error is not null) throw new ReglaNegocioException("VALIDACION", error);

        var motivo = ReglasReporte.LeerMotivo(req.Motivo)!;
        var descripcion = ReglasSeguridadIa.Sanear(req.Descripcion!);   // sin caracteres de control (el admin lo lee en el panel)

        if (ReglasReporte.ObjetoDelMotivo(motivo) is { } objeto)
        {
            var id = (objeto switch
            {
                ReglasReporte.ObjetoLugar => req.LugarId,
                ReglasReporte.ObjetoComunidad => req.ComunidadId,
                _ => req.EventoId
            })!.Value;

            if (!await _repo.ObjetoExisteAsync(objeto, id, ct))
                throw new ReglaNegocioException("NO_ENCONTRADO", objeto switch
                {
                    ReglasReporte.ObjetoLugar => ReglasReporte.MensajeLugarNoExiste,
                    ReglasReporte.ObjetoComunidad => ReglasReporte.MensajeComunidadNoExiste,
                    _ => ReglasReporte.MensajeEventoNoExiste
                });
            if (await _repo.TienePendienteSobreAsync(usuarioId, objeto, id, ct))
                throw new ReglaNegocioException("YA_REPORTADO", ReglasReporte.MensajeYaReportadoDe(objeto));
        }

        if (await _repo.ContarPendientesAsync(usuarioId, ct) >= ReglasReporte.MaximoPendientesPorEstudiante)
            throw new ReglaNegocioException("DEMASIADOS_PENDIENTES", ReglasReporte.MensajeDemasiados);

        var nuevo = await _repo.CrearAsync(usuarioId, motivo, req.LugarId, req.ComunidadId, req.EventoId, descripcion, ct);
        return new ReporteCreadoDto(nuevo, ReglasReporte.Pendiente);
    }
}
