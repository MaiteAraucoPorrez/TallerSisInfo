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
    Task GuardarCambiosAsync(Evento evento, byte[]? imagen, string? tipoMime, CancellationToken ct = default);
    Task<FotoEvento?> ObtenerImagenAsync(int eventoId, CancellationToken ct = default);
    Task<List<EventoListado>> ListarPendientesAsync(CancellationToken ct = default);
    Task<ResultadoModeracionEvento> ModerarAsync(
        int eventoId,
        string estado,
        string? motivo,
        DateTime ahoraUtc,
        CancellationToken ct = default);
}
