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
    int? VisitaMax,
    // FORMULARIO (por defecto) o FLYER cuando el borrador salió de leer un afiche. Solo se usa al crear.
    string? Origen = null,
    // Solo al editar: borra la foto actual del evento. No se puede combinar con una foto nueva. Al crear se ignora.
    bool QuitarFoto = false);

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

public sealed record EventoParaPuente(Evento Evento, Lugar Lugar);

public sealed record EventoEnPuenteDto(
    int Id,
    string Titulo,
    string Inicio,
    string Fin,
    string Lugar,
    string Asistencia,
    int? MinutosPuedeQuedarse,
    string Explicacion);

public sealed record FotoEvento(
    byte[] Contenido,
    string TipoMime,
    DateTime ActualizadaEn,
    int CreadorId,
    string Estado,
    bool CreadorSuspendido);

public sealed record ResolverEventoRequest(string? Estado, string? Motivo);

public enum ResultadoModeracionEvento
{
    Actualizado,
    NoEncontrado,
    YaRevisado,
    Finalizado
}

public sealed record EventoPublicoDto(
    int Id,
    string Titulo,
    string? Descripcion,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    int? LugarId,
    string Lugar,
    bool FueraDelCampus,
    string Asistencia,
    short? VisitaMin,
    short? VisitaMax,
    string? FotoUrl);
