namespace Breaku.Application.Reportes;

/// <summary>Reporte que envía el estudiante: desde su perfil (generales y de lugares) o desde una comunidad o un evento.</summary>
/// <param name="Motivo" example="LUGAR_INCORRECTO"><c>ERROR_APP</c> (algo no funciona), <c>SUGERENCIA</c>, <c>LUGAR_INCORRECTO</c>, <c>COMUNIDAD_INAPROPIADA</c> o <c>EVENTO_INAPROPIADO</c>.</param>
/// <param name="LugarId" example="4">Solo con <c>LUGAR_INCORRECTO</c>: el lugar (de <c>GET /api/reportes/lugares</c>).</param>
/// <param name="Descripcion" example="El horario dice que abre a las 7, pero abre a las 9.">De 10 a 500 caracteres.</param>
/// <param name="ComunidadId" example="31">Solo con <c>COMUNIDAD_INAPROPIADA</c>: la comunidad reportada.</param>
/// <param name="EventoId" example="8">Solo con <c>EVENTO_INAPROPIADO</c>: el evento reportado.</param>
public record NuevoReporteDto(string? Motivo, int? LugarId, string? Descripcion, int? ComunidadId = null, int? EventoId = null);

/// <summary>Reporte recibido.</summary>
/// <param name="Id" example="12">Identificador del reporte.</param>
/// <param name="Estado" example="PENDIENTE">Siempre <c>PENDIENTE</c>: lo revisa un administrador.</param>
public record ReporteCreadoDto(int Id, string Estado);

/// <summary>Lugar que se puede reportar.</summary>
public record LugarReportableDto(int Id, string Nombre);

/// <summary>Un reporte propio, para «Mis reportes».</summary>
/// <param name="Id" example="12">Identificador del reporte.</param>
/// <param name="Motivo" example="EVENTO_INAPROPIADO">El motivo que se eligió.</param>
/// <param name="ObjetoTipo" example="EVENTO"><c>LUGAR</c>, <c>COMUNIDAD</c> o <c>EVENTO</c>; <c>null</c> en «algo no funciona» y sugerencias.</param>
/// <param name="ObjetoId" example="8">El id del lugar, la comunidad o el evento; <c>null</c> si no apunta a nada.</param>
/// <param name="Objeto" example="Feria de clubes">El nombre del lugar, la actividad de la comunidad o el título del evento.</param>
/// <param name="Descripcion" example="El afiche tiene insultos.">Lo que escribió el estudiante.</param>
/// <param name="Estado" example="RESUELTO"><c>PENDIENTE</c>, <c>RESUELTO</c> o <c>DESCARTADO</c>.</param>
/// <param name="CreadoEn" example="2026-11-15T14:20:00Z">Cuándo se envió (UTC).</param>
/// <param name="RevisadoEn" example="2026-11-15T16:02:00Z">Cuándo lo revisó un administrador (UTC); <c>null</c> si sigue pendiente.</param>
/// <param name="Respuesta" example="Gracias, ya bajamos el evento.">La respuesta del administrador, si dejó una; <c>null</c> si no.</param>
public record MiReporteDto(
    int Id,
    string Motivo,
    string? ObjetoTipo,
    int? ObjetoId,
    string? Objeto,
    string? Descripcion,
    string Estado,
    DateTime CreadoEn,
    DateTime? RevisadoEn,
    string? Respuesta);
