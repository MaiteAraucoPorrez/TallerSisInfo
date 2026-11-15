using Breaku.Domain.Rules;

namespace Breaku.Application.Eventos;

public record ImagenFlyer(byte[] Bytes, string TipoMime);

/// <summary>Lo que se le pide al lector: imágenes (foto o páginas del PDF) o texto pegado, y las instrucciones ya armadas.</summary>
public record SolicitudLecturaEvento(string Instrucciones, IReadOnlyList<ImagenFlyer> Imagenes, string? Texto);

public record LecturaEvento(string Modelo, string TextoCrudo, EventoLeido Evento);

public interface ILectorEventoFlyer
{
    Task<LecturaEvento> LeerAsync(SolicitudLecturaEvento solicitud, CancellationToken ct = default);
}
