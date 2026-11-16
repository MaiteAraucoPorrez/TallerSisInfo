using Breaku.Domain;

namespace Breaku.Application.Eventos;

public sealed record EventoFormularioRequest(
    string? Titulo,
    string? Descripcion,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fin,
    int? LugarId,
    string? LugarTexto,
    string? Asistencia,
    int? VisitaMin,
    int? VisitaMax);

public sealed record EventoDto(
    int Id,
    string Titulo,
    string? Descripcion,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    int? LugarId,
    string Lugar,
    string Estado,
    string Origen,
    string Asistencia,
    short? VisitaMin,
    short? VisitaMax,
    string? Motivo,
    string? FotoUrl,
    DateTimeOffset CreatedAt);

public sealed record EventoLugarDto(int Id, string Nombre);

public sealed record EventoListado(Evento Evento, string NombreLugar, DateTime? FotoActualizadaEn);

public sealed record FotoEvento(byte[] Contenido, string TipoMime, DateTime ActualizadaEn, int CreadorId, string Estado);

public sealed record ResolverEventoRequest(string? Estado, string? Motivo);

public enum ResultadoModeracionEvento
{
    Actualizado,
    NoEncontrado,
    YaRevisado
}
