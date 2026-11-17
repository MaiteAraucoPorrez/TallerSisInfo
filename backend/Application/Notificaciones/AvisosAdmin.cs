using Breaku.Application.TiempoReal;
using Breaku.Domain;

namespace Breaku.Application.Notificaciones;

/// <summary>Lo que el admin tiene que revisar y a qué apunta el aviso (un reporte o un evento).</summary>
public enum ObjetoAvisoAdmin { Reporte, Evento }

/// <summary>Guarda los avisos de la campanita de los administradores.</summary>
public interface IAvisosAdminRepository
{
    /// <summary>
    /// Un aviso por cada admin activo (si ya tenía uno por ese mismo objeto, vuelve a quedar sin leer con el texto nuevo).
    /// Devuelve a quiénes les llegó.
    /// </summary>
    Task<IReadOnlyList<int>> AvisarAsync(string tipo, ObjetoAvisoAdmin objeto, int objetoId, string mensaje, CancellationToken ct = default);

    /// <summary>Marca como leídos los avisos de ese objeto de todos los admins. Devuelve a quiénes les cambió algo.</summary>
    Task<IReadOnlyList<int>> CerrarAsync(string tipo, ObjetoAvisoAdmin objeto, int objetoId, CancellationToken ct = default);
}

/// <summary>
/// Avisos para el administrador: reportes nuevos y eventos que esperan revisión. Nunca rompen la operación del
/// estudiante (si la base todavía no tiene la migración 030 o falla el canal en vivo, el reporte o el evento igual se
/// guardan; el admin los sigue viendo en el panel).
/// </summary>
public interface IAvisosAdmin
{
    Task ReporteNuevoAsync(int reporteId, string motivo, string? descripcion, CancellationToken ct = default);
    Task ReporteRevisadoAsync(int reporteId, CancellationToken ct = default);
    Task EventoPorRevisarAsync(int eventoId, string titulo, string? motivo, CancellationToken ct = default);
    Task EventoRevisadoAsync(int eventoId, CancellationToken ct = default);
}

public sealed class AvisosAdminService : IAvisosAdmin
{
    private readonly IAvisosAdminRepository _repo;
    private readonly ITiempoReal _tiempoReal;

    public AvisosAdminService(IAvisosAdminRepository repo, ITiempoReal tiempoReal)
    {
        _repo = repo;
        _tiempoReal = tiempoReal;
    }

    public Task ReporteNuevoAsync(int reporteId, string motivo, string? descripcion, CancellationToken ct = default)
        => Seguro(() => _repo.AvisarAsync(Notificacion.TipoReporteNuevo, ObjetoAvisoAdmin.Reporte, reporteId,
            ReglasAvisosAdmin.MensajeReporteNuevo(motivo, descripcion), ct), ct);

    public Task ReporteRevisadoAsync(int reporteId, CancellationToken ct = default)
        => Seguro(() => _repo.CerrarAsync(Notificacion.TipoReporteNuevo, ObjetoAvisoAdmin.Reporte, reporteId, ct), ct);

    public Task EventoPorRevisarAsync(int eventoId, string titulo, string? motivo, CancellationToken ct = default)
        => Seguro(() => _repo.AvisarAsync(Notificacion.TipoEventoPorRevisar, ObjetoAvisoAdmin.Evento, eventoId,
            ReglasAvisosAdmin.MensajeEventoPorRevisar(titulo, motivo), ct), ct);

    public Task EventoRevisadoAsync(int eventoId, CancellationToken ct = default)
        => Seguro(() => _repo.CerrarAsync(Notificacion.TipoEventoPorRevisar, ObjetoAvisoAdmin.Evento, eventoId, ct), ct);

    private async Task Seguro(Func<Task<IReadOnlyList<int>>> accion, CancellationToken ct)
    {
        try
        {
            var admins = await accion();
            if (admins.Count > 0) await _tiempoReal.AvisosCambiaronAsync(admins, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El aviso al admin es un extra: lo importante (el reporte, el evento, la decisión) ya quedó guardado.
        }
    }
}
