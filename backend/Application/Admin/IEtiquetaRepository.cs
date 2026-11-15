namespace Breaku.Application.Admin;

/// <summary>HU30: catálogo de etiquetas de tipo de actividad y lo que necesita saber el admin de un lugar para activarlo.</summary>
public interface IEtiquetaRepository
{
    /// <summary>El catálogo, por nombre.</summary>
    Task<List<EtiquetaDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>true si ya hay una etiqueta con ese nombre (sin distinguir mayúsculas ni tildes, como la base).</summary>
    Task<bool> NombreEnUsoAsync(string nombre, CancellationToken ct = default);

    /// <summary>Agrega una etiqueta al catálogo y devuelve su id.</summary>
    Task<int> CrearAsync(string nombre, CancellationToken ct = default);

    /// <summary>Estado, si es recomendable y cuántas etiquetas tiene el lugar; <c>null</c> si no existe.</summary>
    Task<EstadoEtiquetasLugar?> EstadoDelLugarAsync(int lugarId, CancellationToken ct = default);
}

public record EstadoEtiquetasLugar(string Estado, bool Recomendable, int Etiquetas);
