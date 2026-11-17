using Breaku.Domain;

namespace Breaku.Application.Eventos;

public interface IEventoRepository
{
    Task<List<Lugar>> ListarLugaresDisponiblesAsync(CancellationToken ct = default);
    Task<Lugar?> ObtenerLugarAsync(int lugarId, CancellationToken ct = default);
    Task<List<EventoParaPuente>> ListarPublicadosHoyAsync(
        DateTime desdeUtc,
        DateTime hastaUtc,
        DateTime ahoraUtc,
        CancellationToken ct = default);
    Task<bool> CrearAsync(
        Evento evento,
        byte[]? imagen,
        string? tipoMime,
        bool aplicarLimiteDiario,
        DateTime desdeUtc,
        DateTime hastaUtc,
        CancellationToken ct = default);
    Task<List<EventoListado>> ListarMiosAsync(int usuarioId, CancellationToken ct = default);
    Task<Evento?> ObtenerMioAsync(int eventoId, int usuarioId, CancellationToken ct = default);
    /// <summary>Guarda los cambios del evento. Una imagen nueva reemplaza la anterior; `quitarImagen` la borra (si no hay, no hace nada).</summary>
    Task GuardarCambiosAsync(Evento evento, byte[]? imagen, string? tipoMime, bool quitarImagen = false, CancellationToken ct = default);
    Task<FotoEvento?> ObtenerImagenAsync(int eventoId, CancellationToken ct = default);
    /// <summary>Eventos PUBLICADOS que no terminaron y empiezan antes de hastaUtc, sin los de creadores suspendidos.</summary>
    Task<List<EventoListado>> ListarPublicadosAsync(DateTime ahoraUtc, DateTime hastaUtc, CancellationToken ct = default);
    Task<EventoListado?> ObtenerPublicadoAsync(int eventoId, DateTime ahoraUtc, CancellationToken ct = default);

    Task<List<EventoListado>> ListarPendientesAsync(DateTime ahoraUtc, CancellationToken ct = default);
    Task<ResultadoModeracionEvento> ModerarAsync(
        int eventoId,
        string estado,
        string? motivo,
        DateTime ahoraUtc,
        CancellationToken ct = default);
}
