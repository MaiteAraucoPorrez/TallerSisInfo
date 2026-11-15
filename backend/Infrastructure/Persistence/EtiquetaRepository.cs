using Breaku.Application.Admin;
using Breaku.Application.Horarios;
using Breaku.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Breaku.Infrastructure.Persistence;

/// <summary>HU30: etiquetas de tipo de actividad (las tablas etiqueta_actividad y lugar_etiqueta no están mapeadas en EF).</summary>
public class EtiquetaRepository : IEtiquetaRepository
{
    private readonly AppDbContext _db;
    public EtiquetaRepository(AppDbContext db) => _db = db;

    public async Task<List<EtiquetaDto>> ListarAsync(CancellationToken ct = default)
        => (await _db.Database.SqlQuery<FilaEtiqueta>($"SELECT id AS Id, nombre AS Nombre FROM etiqueta_actividad ORDER BY nombre")
                .ToListAsync(ct))
            .Select(f => new EtiquetaDto(f.Id, f.Nombre)).ToList();

    public async Task<bool> NombreEnUsoAsync(string nombre, CancellationToken ct = default)
        => (await _db.Database.SqlQuery<long>($"SELECT COUNT(*) AS Value FROM etiqueta_actividad WHERE nombre = {nombre}")
            .ToListAsync(ct)).Single() > 0;

    public async Task<int> CrearAsync(string nombre, CancellationToken ct = default)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.Database.ExecuteSqlAsync($"INSERT INTO etiqueta_actividad (nombre) VALUES ({nombre})", ct);
            var id = (int)(await _db.Database.SqlQuery<ulong>($"SELECT LAST_INSERT_ID() AS Value").ToListAsync(ct)).Single();
            await tx.CommitAsync(ct);
            return id;
        }
        catch (Exception ex) when ((ex as MySqlException ?? ex.InnerException as MySqlException) is { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            // Dos admins crean la misma a la vez: uq_etiqueta_nombre deja pasar solo una.
            throw new ReglaNegocioException("NOMBRE_REPETIDO", ReglasEtiquetas.MensajeRepetida);
        }
    }

    public async Task<EstadoEtiquetasLugar?> EstadoDelLugarAsync(int lugarId, CancellationToken ct = default)
    {
        var fila = (await _db.Database.SqlQuery<FilaEstado>($"""
                SELECT l.estado AS Estado, l.recomendable AS Recomendable,
                       (SELECT COUNT(*) FROM lugar_etiqueta le WHERE le.lugar_id = l.id) AS Etiquetas
                FROM lugar l WHERE l.id = {lugarId}
                """).ToListAsync(ct)).FirstOrDefault();
        return fila is null ? null : new EstadoEtiquetasLugar(fila.Estado, fila.Recomendable, (int)fila.Etiquetas);
    }

    private sealed class FilaEtiqueta
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
    }

    private sealed class FilaEstado
    {
        public string Estado { get; set; } = "";
        public bool Recomendable { get; set; }
        public long Etiquetas { get; set; }
    }
}
