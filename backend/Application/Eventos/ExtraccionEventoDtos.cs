namespace Breaku.Application.Eventos;

/// <summary>Borrador de evento. inicio y fin van en hora de Bolivia, sin zona ("yyyy-MM-ddTHH:mm"); HU23 los guarda en UTC.</summary>
public record BorradorEventoDto(
    string? Titulo,
    string? Descripcion,
    string? Inicio,
    string? Fin,
    string? LugarTexto,
    string? Asistencia,
    int? VisitaMin,
    int? VisitaMax);

/// <summary>Página 1 del PDF como JPEG, solo si cabe en 2 MB (la imagen del evento de HU23).</summary>
public record ImagenEventoDto(string TipoMime, string ContenidoBase64, int Bytes);

public record RespuestaExtraccionEventoDto(
    long ConsultaId,
    string Estado,
    string? Mensaje,
    BorradorEventoDto Evento,
    List<string> Faltantes,
    List<string> Advertencias,
    ImagenEventoDto? Imagen);
