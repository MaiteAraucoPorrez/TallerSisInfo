using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Application.Recomendaciones;

public interface IRecuperadorLugares
{
    Task<ContextoRecuperado> RecuperarAsync(int usuarioId, Puente puente, CancellationToken ct = default);
}

/// <param name="Personalizado">HU30: si el orden salió del historial del estudiante (3 feedbacks o más).</param>
/// <param name="Preferencias">HU30: lo que se le cuenta al LLM (solo etiquetas); null si no personaliza.</param>
public record ContextoRecuperado(int? ZonaId, int? RadioAplicadoM, List<LugarCandidato> Lugares,
    bool Personalizado = false, ReglasAfinidad.ResumenPreferencias? Preferencias = null);

public record LugarCandidato(
    int LugarId,
    string Nombre,
    string Categoria,
    string? Descripcion,
    int DistanciaM,
    int TiempoTrasladoMin,
    int DuracionEstimadaMin,
    bool Validado = true,
    string? TipoLugar = null,
    List<string>? Modalidad = null,
    List<string>? NivelEnergia = null,
    List<string>? ActividadesSugeridas = null,
    string? FotoUrl = null,
    decimal? Puntuacion = null,
    int Votos = 0,
    // HU30: los tipos de actividad del lugar y su puntaje de afinidad con el estudiante (0 sin personalización).
    IReadOnlyList<string>? Etiquetas = null,
    int Afinidad = 0
);
