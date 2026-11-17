using Breaku.Application.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace Breaku.Infrastructure.Persistence;

/// <summary>Avisos de la campanita de los administradores (tipos REPORTE_NUEVO y EVENTO_POR_REVISAR, migración 030).</summary>
public class AvisosAdminRepository : IAvisosAdminRepository
{
    private readonly AppDbContext _db;
    public AvisosAdminRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<int>> AvisarAsync(string tipo, ObjetoAvisoAdmin objeto, int objetoId, string mensaje,
        CancellationToken ct = default)
    {
        // uq_notif_reporte / uq_notif_evento (usuario, objeto, tipo): si el admin ya tenía el aviso (por ejemplo, el
        // evento volvió a revisión después de editarse), se reactiva como nuevo en vez de duplicarse.
        if (objeto == ObjetoAvisoAdmin.Reporte)
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO notificacion (usuario_id, tipo, reporte_id, mensaje)
                SELECT u.id, {tipo}, {objetoId}, {mensaje} FROM usuario u WHERE u.rol = 'ADMIN' AND u.activo = TRUE
                ON DUPLICATE KEY UPDATE mensaje = {mensaje}, leida = FALSE, activa = TRUE, created_at = UTC_TIMESTAMP(6)
                """, ct);
        else
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO notificacion (usuario_id, tipo, evento_id, mensaje)
                SELECT u.id, {tipo}, {objetoId}, {mensaje} FROM usuario u WHERE u.rol = 'ADMIN' AND u.activo = TRUE
                ON DUPLICATE KEY UPDATE mensaje = {mensaje}, leida = FALSE, activa = TRUE, created_at = UTC_TIMESTAMP(6)
                """, ct);

        return await _db.Database
            .SqlQuery<int>($"SELECT id AS Value FROM usuario WHERE rol = 'ADMIN' AND activo = TRUE")
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> CerrarAsync(string tipo, ObjetoAvisoAdmin objeto, int objetoId, CancellationToken ct = default)
    {
        var consulta = objeto == ObjetoAvisoAdmin.Reporte
            ? _db.Notificaciones.Where(n => n.Tipo == tipo && n.ReporteId == objetoId && n.Activa && !n.Leida)
            : _db.Notificaciones.Where(n => n.Tipo == tipo && n.EventoId == objetoId && n.Activa && !n.Leida);

        var admins = await consulta.Select(n => n.UsuarioId).Distinct().ToListAsync(ct);
        if (admins.Count > 0) await consulta.ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true), ct);
        return admins;
    }
}
