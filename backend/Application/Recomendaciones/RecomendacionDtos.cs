namespace Breaku.Application.Recomendaciones;

public record SolicitudRecomendacionDto(int PuenteId, string? Entrada);

public record RespuestaRecomendacionDto(
    long ConsultaId,
    string Estado,
    string? Mensaje,
    List<RecomendacionDto> Recomendaciones
);

public record RecomendacionDto(
    long Id,
    int LugarId,
    string Nombre,
    string Categoria,
    int DistanciaM,
    int TiempoTrasladoMin,
    int DuracionEstimadaMin,
    string Explicacion,
    string? Feedback = null,
    string? FotoUrl = null,
    /// <summary>De 1 a 5, a partir de los votos de los estudiantes; null si nadie votó.</summary>
    decimal? Puntuacion = null,
    int Votos = 0,
    /// <summary>El lugar fue verificado por el equipo (<c>lugar.validado</c>).</summary>
    bool Verificado = false,
    /// <summary>HU30: tipos de actividad del lugar que al estudiante le suelen gustar («Porque te gusta…»). Vacío sin personalización.</summary>
    IReadOnlyList<string>? PorQueTeGusta = null
);

public record FeedbackRecomendacionDto(string? Feedback);

public record FeedbackGuardadoDto(long RecomendacionId, string Feedback, DateTime FechaFeedback);
