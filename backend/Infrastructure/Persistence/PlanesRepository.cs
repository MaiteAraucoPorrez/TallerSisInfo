using Breaku.Application.Matching;
using Breaku.Domain;
using Breaku.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Breaku.Infrastructure.Persistence;

/// <summary>Planes con conexiones e invitaciones de «Libres contigo» (migración 032).</summary>
public sealed class PlanesRepository : IPlanesRepository
{
    private readonly AppDbContext _db;
    public PlanesRepository(AppDbContext db) => _db = db;

    public async Task<List<ComunidadMia>> ListarMisComunidadesAsync(int usuarioId, DateTime ahoraUtc, DateTime hastaUtc,
        CancellationToken ct = default)
    {
        var comunidades = await _db.Comunidades.AsNoTracking()
            .Where(c => c.CreadorId == usuarioId && c.Estado == ComunidadEfimera.EstadoActiva && c.Fin > ahoraUtc && c.Inicio < hastaUtc)
            .OrderBy(c => c.Inicio).ThenBy(c => c.Id)
            .ToListAsync(ct);
        if (comunidades.Count == 0) return [];

        var ids = comunidades.Select(c => c.Id).ToList();
        var filas = await _db.Set<Participacion>().AsNoTracking()
            .Where(p => ids.Contains(p.ComunidadId))
            .Select(p => new { p.ComunidadId, p.UsuarioId, p.Estado })
            .ToListAsync(ct);

        return comunidades.Select(c =>
        {
            var suyas = filas.Where(f => f.ComunidadId == c.Id).ToList();
            var confirmados = ReglasDescubrir.Confirmados(
                suyas.Count(f => f.Estado == Participacion.EstadoConfirmado), suyas.Any(f => f.UsuarioId == c.CreadorId));
            return new ComunidadMia(c, confirmados, suyas.Select(f => f.UsuarioId).ToHashSet());
        }).ToList();
    }

    public Task<string?> CruceAsync(int usuarioId, DateTime inicioUtc, DateTime finUtc, CancellationToken ct = default)
    {
        var participaciones = _db.Set<Participacion>();
        return _db.Comunidades.AsNoTracking()
            .Where(c => c.Estado == ComunidadEfimera.EstadoActiva && c.Inicio < finUtc && c.Fin > inicioUtc
                        && (c.CreadorId == usuarioId
                            || participaciones.Any(p => p.ComunidadId == c.Id && p.UsuarioId == usuarioId
                                                        && p.Estado == Participacion.EstadoConfirmado)))
            .OrderBy(c => c.Inicio)
            .Select(c => c.Actividad)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<(DateTime InicioUtc, DateTime FinUtc)>> OcupadosAsync(IReadOnlyCollection<int> usuarioIds, DateTime desdeUtc,
        DateTime hastaUtc, CancellationToken ct = default)
    {
        var participaciones = _db.Set<Participacion>();
        var filas = await _db.Comunidades.AsNoTracking()
            .Where(c => c.Estado == ComunidadEfimera.EstadoActiva && c.Fin > desdeUtc && c.Inicio < hastaUtc
                        && (usuarioIds.Contains(c.CreadorId)
                            || participaciones.Any(p => p.ComunidadId == c.Id && usuarioIds.Contains(p.UsuarioId)
                                                        && p.Estado == Participacion.EstadoConfirmado)))
            .Select(c => new { c.Inicio, c.Fin })
            .ToListAsync(ct);
        return filas.Select(f => (f.Inicio, f.Fin)).ToList();
    }

    public Task<bool> QuiereAvisosAsync(int usuarioId, CancellationToken ct = default)
        => _db.Usuarios.AnyAsync(u => u.Id == usuarioId && u.Activo && u.NotificacionesActivas, ct);

    public async Task<int> CrearPlanAsync(ComunidadEfimera plan, int invitadoId, string? aviso, DateTime ahoraUtc,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Comunidades.Add(plan);
        await _db.SaveChangesAsync(ct);

        _db.Set<Participacion>().AddRange(
            new Participacion
            {
                ComunidadId = plan.Id, UsuarioId = plan.CreadorId, Estado = Participacion.EstadoConfirmado,
                InvitadoEn = ahoraUtc, RespondidoEn = ahoraUtc
            },
            new Participacion { ComunidadId = plan.Id, UsuarioId = invitadoId, Estado = Participacion.EstadoPendiente, InvitadoEn = ahoraUtc });
        if (aviso is not null)
            _db.Notificaciones.Add(new Notificacion
            {
                UsuarioId = invitadoId, Tipo = Notificacion.TipoInvitacionComunidad, ComunidadId = plan.Id, Mensaje = aviso,
                CreatedAt = ahoraUtc
            });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return plan.Id;
    }

    public async Task<bool> InvitarAComunidadAsync(int comunidadId, int invitadoId, string? aviso, DateTime ahoraUtc,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var participaciones = _db.Set<Participacion>();
        if (await participaciones.AnyAsync(p => p.ComunidadId == comunidadId && p.UsuarioId == invitadoId, ct)) return false;

        participaciones.Add(new Participacion
        {
            ComunidadId = comunidadId, UsuarioId = invitadoId, Estado = Participacion.EstadoPendiente, InvitadoEn = ahoraUtc
        });
        await _db.SaveChangesAsync(ct);
        if (aviso is not null)
            await _db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO notificacion (usuario_id, tipo, comunidad_id, mensaje, leida, activa, created_at)
                 VALUES ({invitadoId}, {Notificacion.TipoInvitacionComunidad}, {comunidadId}, {aviso}, FALSE, TRUE, {ahoraUtc})
                 ON DUPLICATE KEY UPDATE mensaje = VALUES(mensaje), leida = FALSE, activa = TRUE, created_at = VALUES(created_at)
                 """, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public Task InvitarAEventoAsync(int eventoId, int invitadoId, string aviso, DateTime ahoraUtc, CancellationToken ct = default)
        => _db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO notificacion (usuario_id, tipo, evento_id, mensaje, leida, activa, created_at)
             VALUES ({invitadoId}, {Notificacion.TipoInvitacionEvento}, {eventoId}, {aviso}, FALSE, TRUE, {ahoraUtc})
             ON DUPLICATE KEY UPDATE mensaje = VALUES(mensaje), leida = FALSE, activa = TRUE, created_at = VALUES(created_at)
             """, ct);
}
