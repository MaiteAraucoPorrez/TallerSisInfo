using Breaku.Application.Lugares;
using Breaku.Domain;
using Breaku.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Breaku.Infrastructure.Persistence;

public class LugarRepository : ILugarRepository
{
    private readonly AppDbContext _db;
    public LugarRepository(AppDbContext db) => _db = db;

    public Task<Zona?> ObtenerZonaDelUsuarioAsync(int usuarioId, CancellationToken ct = default)
        => _db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId && u.ZonaActualId != null && u.ZonaActual!.Activa)
            .Select(u => u.ZonaActual)
            .FirstOrDefaultAsync(ct);

    public Task<List<Lugar>> ListarActivosAsync(CancellationToken ct = default)
        => _db.Lugares.AsNoTracking()
            .Include(l => l.Categoria)
            .Where(l => l.Estado == ReglasBusquedaLugares.EstadoActivo && l.Recomendable)
            .ToListAsync(ct);

    public async Task<Dictionary<int, (int MeGusta, int NoMeSirve)>> ContarVotosAsync(IReadOnlyCollection<int> lugarIds, CancellationToken ct = default)
    {
        if (lugarIds.Count == 0) return [];
        var votos = await _db.Recomendaciones.AsNoTracking()
            .Where(r => lugarIds.Contains(r.LugarId) && r.Feedback != null)
            .GroupBy(r => r.LugarId)
            .Select(g => new
            {
                LugarId = g.Key,
                MeGusta = g.Count(r => r.Feedback == ReglasFeedback.MeGusta),
                NoMeSirve = g.Count(r => r.Feedback == ReglasFeedback.NoMeSirve),
            })
            .ToListAsync(ct);
        return votos.ToDictionary(v => v.LugarId, v => (v.MeGusta, v.NoMeSirve));
    }

    public async Task<Dictionary<int, List<int>>> ListarEtiquetasDeLugaresAsync(IReadOnlyCollection<int> lugarIds, CancellationToken ct = default)
    {
        if (lugarIds.Count == 0) return [];
        // lugar_etiqueta no está mapeada en EF: SQL con la lista de ids como parámetros (nunca concatenados).
        var ids = string.Join(",", lugarIds.Select((_, i) => "{" + i + "}"));
        var filas = await _db.Database
            .SqlQueryRaw<FilaEtiquetaLugar>(
                $"SELECT lugar_id AS LugarId, etiqueta_id AS EtiquetaId FROM lugar_etiqueta WHERE lugar_id IN ({ids})",
                lugarIds.Select(id => (object)id).ToArray())
            .ToListAsync(ct);
        return filas.GroupBy(f => f.LugarId).ToDictionary(g => g.Key, g => g.Select(f => f.EtiquetaId).Order().ToList());
    }

    public async Task<List<ReglasAfinidad.VotoLugar>> ListarVotosDelUsuarioAsync(int usuarioId, CancellationToken ct = default)
    {
        var votos = await _db.Database.SqlQuery<FilaVoto>($"""
                SELECT r.lugar_id AS LugarId, r.feedback AS Feedback, r.fecha_feedback AS Fecha
                FROM recomendacion r
                JOIN log_auditoria_rag g ON g.id = r.log_id
                WHERE g.usuario_id = {usuarioId} AND r.feedback IS NOT NULL AND r.fecha_feedback IS NOT NULL
                """).ToListAsync(ct);
        if (votos.Count == 0) return [];

        var etiquetas = await ListarEtiquetasDeLugaresAsync(votos.Select(v => v.LugarId).Distinct().ToList(), ct);
        return votos.Select(v => new ReglasAfinidad.VotoLugar(v.LugarId, v.Feedback,
                DateTime.SpecifyKind(v.Fecha, DateTimeKind.Utc), etiquetas.GetValueOrDefault(v.LugarId) ?? []))
            .ToList();
    }

    public async Task<Dictionary<int, string>> ListarNombresEtiquetasAsync(CancellationToken ct = default)
        => (await _db.Database.SqlQuery<FilaEtiqueta>($"SELECT id AS Id, nombre AS Nombre FROM etiqueta_actividad").ToListAsync(ct))
            .ToDictionary(f => f.Id, f => f.Nombre);

    private sealed class FilaEtiquetaLugar
    {
        public int LugarId { get; set; }
        public int EtiquetaId { get; set; }
    }

    private sealed class FilaVoto
    {
        public int LugarId { get; set; }
        public string Feedback { get; set; } = "";
        public DateTime Fecha { get; set; }
    }

    private sealed class FilaEtiqueta
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
    }
}
