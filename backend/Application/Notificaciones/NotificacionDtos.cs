namespace Breaku.Application.Notificaciones;

public record NotificacionDto(
    long Id,
    string Tipo,
    string Mensaje,
    string? Fecha,
    string? HoraInicio,
    string? HoraFin,
    int? DuracionMinutos,
    bool Leida,
    DateTime CreadaEn,
    int? ComunidadId = null,
    // HU27: en REPORTE_RESUELTO, el reporte (la campanita lleva a «Mis reportes»).
    int? ReporteId = null
);

public record ConfiguracionNotificacionesDto(bool NotificacionesActivas);
