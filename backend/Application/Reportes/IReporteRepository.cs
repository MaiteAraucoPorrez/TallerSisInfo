namespace Breaku.Application.Reportes;

/// <summary>Puerto: lo implementa Infrastructure.</summary>
public interface IReporteRepository
{
    /// <summary>Lugares activos del catálogo, por nombre (para elegir cuál reportar).</summary>
    Task<List<LugarReportableDto>> ListarLugaresAsync(CancellationToken ct = default);

    /// <summary>Si existe el lugar, la comunidad o el evento (<c>objeto</c>: <c>LUGAR</c>, <c>COMUNIDAD</c> o <c>EVENTO</c>).</summary>
    Task<bool> ObjetoExisteAsync(string objeto, int id, CancellationToken ct = default);

    /// <summary>Cuántos reportes PENDIENTE tiene el usuario.</summary>
    Task<int> ContarPendientesAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>Si el usuario ya tiene un reporte PENDIENTE de ese lugar, comunidad o evento.</summary>
    Task<bool> TienePendienteSobreAsync(int usuarioId, string objeto, int id, CancellationToken ct = default);

    /// <summary>Guarda el reporte PENDIENTE y devuelve su id.</summary>
    Task<int> CrearAsync(int usuarioId, string motivo, int? lugarId, int? comunidadId, int? eventoId, string descripcion,
        CancellationToken ct = default);

    /// <summary>Los reportes del usuario: primero los pendientes y después los más recientes.</summary>
    Task<List<MiReporteDto>> ListarMiosAsync(int usuarioId, CancellationToken ct = default);
}
