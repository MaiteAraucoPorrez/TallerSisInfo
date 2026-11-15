using Breaku.Application.Reportes;
using Breaku.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Breaku.Infrastructure.Persistence;

/// <summary>Reportes de los estudiantes. Con SQL, como el panel admin que los lee (la tabla reporte no está mapeada).</summary>
public class ReporteRepository : IReporteRepository
{
    private readonly AppDbContext _db;
    public ReporteRepository(AppDbContext db) => _db = db;

    public async Task<List<LugarReportableDto>> ListarLugaresAsync(CancellationToken ct = default)
    {
        var filas = await _db.Database.SqlQuery<FilaLugar>($"""
                SELECT id AS Id, nombre AS Nombre FROM lugar WHERE estado = 'ACTIVO' ORDER BY nombre
                """).ToListAsync(ct);
        return filas.Select(f => new LugarReportableDto(f.Id, f.Nombre)).ToList();
    }

    public async Task<bool> ObjetoExisteAsync(string objeto, int id, CancellationToken ct = default)
    {
        // Una consulta fija por tabla: el nombre de la tabla no puede ir como parámetro.
        var consulta = objeto switch
        {
            ReglasReporte.ObjetoLugar => _db.Database.SqlQuery<long>($"SELECT COUNT(*) AS Value FROM lugar WHERE id = {id}"),
            ReglasReporte.ObjetoComunidad => _db.Database.SqlQuery<long>($"SELECT COUNT(*) AS Value FROM comunidad_efimera WHERE id = {id}"),
            ReglasReporte.ObjetoEvento => _db.Database.SqlQuery<long>($"SELECT COUNT(*) AS Value FROM evento WHERE id = {id}"),
            _ => throw new ArgumentOutOfRangeException(nameof(objeto), objeto, "Objeto de reporte desconocido.")
        };
        return (await consulta.ToListAsync(ct)).Single() > 0;
    }

    public async Task<int> ContarPendientesAsync(int usuarioId, CancellationToken ct = default)
        => (int)(await _db.Database.SqlQuery<long>($"""
                SELECT COUNT(*) AS Value FROM reporte WHERE usuario_id = {usuarioId} AND estado = 'PENDIENTE'
                """).ToListAsync(ct)).Single();

    public async Task<bool> TienePendienteSobreAsync(int usuarioId, string objeto, int id, CancellationToken ct = default)
    {
        var consulta = objeto switch
        {
            ReglasReporte.ObjetoLugar => _db.Database.SqlQuery<long>($"""
                SELECT COUNT(*) AS Value FROM reporte WHERE usuario_id = {usuarioId} AND lugar_id = {id} AND estado = 'PENDIENTE'
                """),
            ReglasReporte.ObjetoComunidad => _db.Database.SqlQuery<long>($"""
                SELECT COUNT(*) AS Value FROM reporte WHERE usuario_id = {usuarioId} AND comunidad_id = {id} AND estado = 'PENDIENTE'
                """),
            ReglasReporte.ObjetoEvento => _db.Database.SqlQuery<long>($"""
                SELECT COUNT(*) AS Value FROM reporte WHERE usuario_id = {usuarioId} AND evento_id = {id} AND estado = 'PENDIENTE'
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(objeto), objeto, "Objeto de reporte desconocido.")
        };
        return (await consulta.ToListAsync(ct)).Single() > 0;
    }

    public async Task<int> CrearAsync(int usuarioId, string motivo, int? lugarId, int? comunidadId, int? eventoId, string descripcion,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO reporte (usuario_id, lugar_id, comunidad_id, evento_id, motivo, descripcion)
                VALUES ({usuarioId}, {lugarId}, {comunidadId}, {eventoId}, {motivo}, {descripcion})
                """, ct);
        var id = (int)(await _db.Database.SqlQuery<ulong>($"SELECT LAST_INSERT_ID() AS Value").ToListAsync(ct)).Single();
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<List<MiReporteDto>> ListarMiosAsync(int usuarioId, CancellationToken ct = default)
    {
        var filas = await _db.Database.SqlQuery<FilaMiReporte>($"""
                SELECT r.id AS Id, r.motivo AS Motivo,
                       CASE WHEN r.lugar_id IS NOT NULL THEN 'LUGAR'
                            WHEN r.comunidad_id IS NOT NULL THEN 'COMUNIDAD'
                            WHEN r.evento_id IS NOT NULL THEN 'EVENTO' END AS ObjetoTipo,
                       COALESCE(r.lugar_id, r.comunidad_id, r.evento_id) AS ObjetoId,
                       COALESCE(l.nombre, c.actividad, e.titulo) AS Objeto,
                       r.descripcion AS Descripcion, r.estado AS Estado, r.created_at AS CreadoEn,
                       r.revisado_en AS RevisadoEn, r.respuesta AS Respuesta
                FROM reporte r
                LEFT JOIN lugar l ON l.id = r.lugar_id
                LEFT JOIN comunidad_efimera c ON c.id = r.comunidad_id
                LEFT JOIN evento e ON e.id = r.evento_id
                WHERE r.usuario_id = {usuarioId}
                ORDER BY r.estado = 'PENDIENTE' DESC, COALESCE(r.revisado_en, r.created_at) DESC, r.id DESC
                LIMIT 100
                """).ToListAsync(ct);
        return filas.Select(f => new MiReporteDto(f.Id, f.Motivo, f.ObjetoTipo, f.ObjetoId, f.Objeto, f.Descripcion, f.Estado,
            DateTime.SpecifyKind(f.CreadoEn, DateTimeKind.Utc),
            f.RevisadoEn is { } r ? DateTime.SpecifyKind(r, DateTimeKind.Utc) : null,
            f.Respuesta)).ToList();
    }

    private sealed class FilaLugar
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
    }

    private sealed class FilaMiReporte
    {
        public int Id { get; set; }
        public string Motivo { get; set; } = "";
        public string? ObjetoTipo { get; set; }
        public int? ObjetoId { get; set; }
        public string? Objeto { get; set; }
        public string? Descripcion { get; set; }
        public string Estado { get; set; } = "";
        public DateTime CreadoEn { get; set; }
        public DateTime? RevisadoEn { get; set; }
        public string? Respuesta { get; set; }
    }
}
