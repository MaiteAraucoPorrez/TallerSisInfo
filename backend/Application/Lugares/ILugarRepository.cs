using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Application.Lugares;

public interface ILugarRepository
{
    Task<Zona?> ObtenerZonaDelUsuarioAsync(int usuarioId, CancellationToken ct = default);

    Task<List<Lugar>> ListarActivosAsync(CancellationToken ct = default);

    /// <summary>Los votos («me gusta», «no me sirve») de las recomendaciones de esos lugares, en una sola consulta.</summary>
    Task<Dictionary<int, (int MeGusta, int NoMeSirve)>> ContarVotosAsync(IReadOnlyCollection<int> lugarIds, CancellationToken ct = default);

    /// <summary>HU30: las etiquetas (ids de <c>lugar_etiqueta</c>) de esos lugares, en una sola consulta. Los que no tienen, no aparecen.</summary>
    Task<Dictionary<int, List<int>>> ListarEtiquetasDeLugaresAsync(IReadOnlyCollection<int> lugarIds, CancellationToken ct = default);

    /// <summary>
    /// HU30: los votos del propio estudiante (recomendaciones suyas con feedback, vía <c>log_auditoria_rag.usuario_id</c>), cada
    /// uno con las etiquetas actuales del lugar votado.
    /// </summary>
    Task<List<ReglasAfinidad.VotoLugar>> ListarVotosDelUsuarioAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>HU30: el catálogo de etiquetas (id → nombre), para el resumen de preferencias.</summary>
    Task<Dictionary<int, string>> ListarNombresEtiquetasAsync(CancellationToken ct = default);
}
