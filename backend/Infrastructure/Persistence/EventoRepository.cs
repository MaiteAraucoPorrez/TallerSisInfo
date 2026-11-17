using Breaku.Application.Eventos;
using Breaku.Application.Horarios;
using Breaku.Domain;
using Breaku.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Breaku.Infrastructure.Persistence;

public sealed class EventoRepository : IEventoRepository
{
    private readonly AppDbContext _db;

    public EventoRepository(AppDbContext db) => _db = db;

    public Task<List<Lugar>> ListarLugaresDisponiblesAsync(CancellationToken ct = default)
        => _db.Lugares.AsNoTracking()
            .Where(l => l.Estado == ReglasBusquedaLugares.EstadoActivo && l.Validado)
            .ToListAsync(ct);

    public Task<Lugar?> ObtenerLugarAsync(int lugarId, CancellationToken ct = default)
        => _db.Lugares.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lugarId, ct);

    public async Task<bool> CrearAsync(
        Evento evento,
        byte[]? imagen,
        string? tipoMime,
        bool aplicarLimiteDiario,
        DateTime desdeUtc,
        DateTime hastaUtc,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (aplicarLimiteDiario)
        {
            var usuarioBloqueado = await _db.Database
                .SqlQuery<int>($"SELECT id AS Value FROM usuario WHERE id = {evento.CreadorId} FOR UPDATE")
                .ToListAsync(ct);
            if (usuarioBloqueado.Count == 0)
                throw new ReglaNegocioException("NO_ENCONTRADO", "No se encontró el estudiante.");

            var creadosHoy = await _db.Eventos.CountAsync(e =>
                e.CreadorId == evento.CreadorId && e.CreatedAt >= desdeUtc && e.CreatedAt < hastaUtc, ct);
            if (creadosHoy >= ReglasEvento.MaximoEventosPorDia) return false;
        }

        _db.Eventos.Add(evento);
        await _db.SaveChangesAsync(ct);
        if (imagen is not null && tipoMime is not null)
            await GuardarImagenAsync(evento.Id, imagen, tipoMime, evento.CreatedAt, ct);

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<List<EventoListado>> ListarMiosAsync(int usuarioId, CancellationToken ct = default)
        => await ListarAsync(_db.Eventos.AsNoTracking().Where(e => e.CreadorId == usuarioId), ct);

    public async Task<Evento?> ObtenerMioAsync(int eventoId, int usuarioId, CancellationToken ct = default)
        => await _db.Eventos.FirstOrDefaultAsync(e => e.Id == eventoId && e.CreadorId == usuarioId, ct);

    public async Task GuardarCambiosAsync(Evento evento, byte[]? imagen, string? tipoMime, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        if (imagen is not null && tipoMime is not null)
            await GuardarImagenAsync(evento.Id, imagen, tipoMime, evento.UpdatedAt, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<FotoEvento?> ObtenerImagenAsync(int eventoId, CancellationToken ct = default)
    {
        var fila = (await _db.Database.SqlQuery<FilaFoto>($"""
                SELECT i.contenido AS Contenido, i.tipo_mime AS TipoMime, i.actualizada_en AS ActualizadaEn,
                       e.creador_id AS CreadorId, e.estado AS Estado
                FROM evento_imagen i
                JOIN evento e ON e.id = i.evento_id
                WHERE i.evento_id = {eventoId}
                """).ToListAsync(ct)).FirstOrDefault();

        if (fila is null) return null;

        var suspendido = await FiltrosSancion.IdsSuspendidos(_db, DateTime.UtcNow)
            .AnyAsync(id => id == fila.CreadorId, ct);

        return new FotoEvento(fila.Contenido, fila.TipoMime,
            DateTime.SpecifyKind(fila.ActualizadaEn, DateTimeKind.Utc), fila.CreadorId, fila.Estado, suspendido);
    }

    public Task<List<EventoListado>> ListarPendientesAsync(DateTime ahoraUtc, CancellationToken ct = default)
        => ListarAsync(_db.Eventos.AsNoTracking()
            .Where(e => e.Estado == Evento.EstadoPendiente && e.Fin > ahoraUtc), ct);

    public async Task<ResultadoModeracionEvento> ModerarAsync(
        int eventoId,
        string estado,
        string? motivo,
        DateTime ahoraUtc,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var bloqueado = await _db.Database
            .SqlQuery<int>($"SELECT id AS Value FROM evento WHERE id = {eventoId} FOR UPDATE")
            .ToListAsync(ct);
        if (bloqueado.Count == 0) return ResultadoModeracionEvento.NoEncontrado;

        var evento = await _db.Eventos.FirstAsync(e => e.Id == eventoId, ct);
        if (evento.Estado != Evento.EstadoPendiente) return ResultadoModeracionEvento.YaRevisado;
        if (evento.Fin <= ahoraUtc) return ResultadoModeracionEvento.Finalizado;

        evento.Estado = estado;
        evento.Motivo = estado == Evento.EstadoRechazado ? motivo : null;
        evento.UpdatedAt = ahoraUtc;
        await _db.SaveChangesAsync(ct);

        var tipo = estado == Evento.EstadoPublicado ? Notificacion.TipoEventoAprobado : Notificacion.TipoEventoRechazado;
        var mensaje = estado == Evento.EstadoPublicado
            ? $"Tu evento «{evento.Titulo}» fue aprobado y publicado."
            : $"Tu evento «{evento.Titulo}» fue rechazado. Revisa el motivo en Mis eventos.";
        if (mensaje.Length > 200) mensaje = mensaje[..200];

        await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO notificacion (usuario_id, tipo, evento_id, mensaje, leida, activa, created_at)
                VALUES ({evento.CreadorId}, {tipo}, {evento.Id}, {mensaje}, FALSE, TRUE, {ahoraUtc})
                ON DUPLICATE KEY UPDATE mensaje = VALUES(mensaje), leida = FALSE, activa = TRUE,
                                        created_at = VALUES(created_at)
                """, ct);
        await tx.CommitAsync(ct);
        return ResultadoModeracionEvento.Actualizado;
    }

    private async Task<List<EventoListado>> ListarAsync(IQueryable<Evento> eventos, CancellationToken ct)
    {
        var filas = await eventos
            .Include(e => e.Lugar)
            .OrderByDescending(e => e.Inicio)
            .Select(e => new
            {
                Evento = e,
                NombreLugar = e.LugarId == null ? e.LugarTexto! : e.Lugar!.Nombre
            })
            .ToListAsync(ct);

        if (filas.Count == 0) return [];
        var ids = filas.Select(f => f.Evento.Id).ToList();
        var fotos = await _db.EventoImagenes.AsNoTracking()
            .Where(i => ids.Contains(i.EventoId))
            .Select(i => new { i.EventoId, i.ActualizadaEn })
            .ToDictionaryAsync(i => i.EventoId, i => (DateTime?)i.ActualizadaEn, ct);

        return filas.Select(f => new EventoListado(
            f.Evento,
            f.NombreLugar,
            fotos.GetValueOrDefault(f.Evento.Id))).ToList();
    }

    private Task<int> GuardarImagenAsync(int eventoId, byte[] contenido, string tipoMime, DateTime actualizadaEn, CancellationToken ct)
        => _db.Database.ExecuteSqlAsync($"""
                INSERT INTO evento_imagen (evento_id, contenido, tipo_mime, bytes, actualizada_en)
                VALUES ({eventoId}, {contenido}, {tipoMime}, {contenido.Length}, {ReglasEvento.VersionImagen(actualizadaEn)})
                ON DUPLICATE KEY UPDATE contenido = VALUES(contenido), tipo_mime = VALUES(tipo_mime),
                                        bytes = VALUES(bytes), actualizada_en = VALUES(actualizada_en)
                """, ct);

    private sealed class FilaFoto
    {
        public byte[] Contenido { get; set; } = [];
        public string TipoMime { get; set; } = "";
        public DateTime ActualizadaEn { get; set; }
        public int CreadorId { get; set; }
        public string Estado { get; set; } = "";
    }
}
