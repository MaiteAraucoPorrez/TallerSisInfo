using Breaku.Domain;
using Breaku.Domain.Rules;
using Breaku.Application.Horarios;

namespace Breaku.Application.Eventos;

public sealed class EventoService
{
    private readonly IEventoRepository _repository;
    private readonly TimeProvider _reloj;

    public EventoService(IEventoRepository repository) : this(repository, TimeProvider.System) { }

    public EventoService(IEventoRepository repository, TimeProvider reloj)
    {
        _repository = repository;
        _reloj = reloj;
    }

    public async Task<List<EventoLugarDto>> ListarLugaresAsync(CancellationToken ct = default)
        => (await _repository.ListarLugaresDisponiblesAsync(ct))
            .OrderBy(l => l.Nombre)
            .Select(l => new EventoLugarDto(l.Id, l.Nombre))
            .ToList();

    public async Task<EventoDto> CrearAsync(
        int usuarioId,
        bool esAdmin,
        EventoFormularioRequest request,
        byte[]? imagen,
        CancellationToken ct = default)
    {
        var (titulo, descripcion, lugarTexto, asistencia, error) = NormalizarYValidar(request);
        if (error is not null) throw new ReglaNegocioException("VALIDACION", error);
        var (tipoMime, errorImagen) = ValidarImagen(imagen);
        if (errorImagen is not null) throw new ReglaNegocioException("VALIDACION", errorImagen);

        var lugar = await ResolverLugarAsync(request.LugarId, lugarTexto, ct);
        var lugarCatalogado = lugar is { Estado: ReglasBusquedaLugares.EstadoActivo, Validado: true };
        var nombreLugar = lugarCatalogado ? lugar!.Nombre : lugarTexto;
        if (string.IsNullOrWhiteSpace(nombreLugar))
            throw new ReglaNegocioException("VALIDACION", "Selecciona un lugar o escribe dónde será el evento.");

        var ahora = _reloj.GetUtcNow();
        var evaluacion = ReglasEvento.Evaluar(
            esAdmin,
            lugarCatalogado,
            titulo,
            descripcion,
            nombreLugar,
            request.Inicio!.Value,
            request.Fin!.Value,
            ahora);

        var evento = new Evento
        {
            CreadorId = usuarioId,
            Titulo = titulo,
            Descripcion = descripcion,
            LugarId = lugarCatalogado ? lugar!.Id : null,
            LugarTexto = lugarCatalogado ? null : nombreLugar.Trim(),
            Inicio = request.Inicio.Value.UtcDateTime,
            Fin = request.Fin.Value.UtcDateTime,
            Estado = evaluacion.Estado,
            Origen = esAdmin ? Evento.OrigenAdmin : Evento.OrigenFormulario,
            Asistencia = asistencia,
            VisitaMin = request.VisitaMin is int min ? (short)min : null,
            VisitaMax = request.VisitaMax is int max ? (short)max : null,
            Motivo = evaluacion.Motivo,
            CreatedAt = ahora.UtcDateTime,
            UpdatedAt = ahora.UtcDateTime
        };

        var (desdeUtc, hastaUtc) = ReglasEvento.RangoDiaLocal(ahora);
        var creado = await _repository.CrearAsync(
            evento, imagen, tipoMime, !esAdmin, desdeUtc, hastaUtc, ct);
        if (!creado) throw new ReglaNegocioException("LIMITE_DIARIO", ReglasEvento.MensajeLimiteDiario);

        return ADto(new EventoListado(evento, nombreLugar, imagen is null ? null : ReglasEvento.VersionImagen(ahora.UtcDateTime)));
    }

    public async Task<List<EventoDto>> ListarMiosAsync(int usuarioId, CancellationToken ct = default)
        => (await _repository.ListarMiosAsync(usuarioId, ct))
            .Select(ADto)
            .ToList();

    public async Task<EventoDto> ActualizarAsync(
        int eventoId,
        int usuarioId,
        EventoFormularioRequest request,
        byte[]? imagen,
        CancellationToken ct = default)
    {
        var evento = await _repository.ObtenerMioAsync(eventoId, usuarioId, ct)
                     ?? throw new ReglaNegocioException("NO_ENCONTRADO", "El evento no existe.");
        var ahora = _reloj.GetUtcNow();
        if (evento.Inicio <= ahora.UtcDateTime)
            throw new ReglaNegocioException("EVENTO_INICIADO", ReglasEvento.MensajeInicioPasado);

        var (titulo, descripcion, lugarTexto, asistencia, error) = NormalizarYValidar(request);
        if (error is not null) throw new ReglaNegocioException("VALIDACION", error);
        var (tipoMime, errorImagen) = ValidarImagen(imagen);
        if (errorImagen is not null) throw new ReglaNegocioException("VALIDACION", errorImagen);

        var lugar = await ResolverLugarAsync(request.LugarId, lugarTexto, ct);
        var lugarCatalogado = lugar is { Estado: ReglasBusquedaLugares.EstadoActivo, Validado: true };
        var nombreLugar = lugarCatalogado ? lugar!.Nombre : lugarTexto;
        if (string.IsNullOrWhiteSpace(nombreLugar))
            throw new ReglaNegocioException("VALIDACION", "Selecciona un lugar o escribe dónde será el evento.");

        var evaluacion = ReglasEvento.Evaluar(
            evento.Origen == Evento.OrigenAdmin,
            lugarCatalogado,
            titulo,
            descripcion,
            nombreLugar,
            request.Inicio!.Value,
            request.Fin!.Value,
            ahora);

        evento.Titulo = titulo;
        evento.Descripcion = descripcion;
        evento.LugarId = lugarCatalogado ? lugar!.Id : null;
        evento.LugarTexto = lugarCatalogado ? null : nombreLugar.Trim();
        evento.Inicio = request.Inicio.Value.UtcDateTime;
        evento.Fin = request.Fin.Value.UtcDateTime;
        evento.Asistencia = asistencia;
        evento.VisitaMin = request.VisitaMin is int min ? (short)min : null;
        evento.VisitaMax = request.VisitaMax is int max ? (short)max : null;
        evento.Estado = evaluacion.Estado;
        evento.Motivo = evaluacion.Motivo;
        evento.UpdatedAt = ahora.UtcDateTime;

        await _repository.GuardarCambiosAsync(evento, imagen, tipoMime, ct);
        var actualizada = (await _repository.ListarMiosAsync(usuarioId, ct))
            .First(e => e.Evento.Id == evento.Id);
        return ADto(actualizada);
    }

    public async Task<EventoDto> CancelarAsync(int eventoId, int usuarioId, CancellationToken ct = default)
    {
        var evento = await _repository.ObtenerMioAsync(eventoId, usuarioId, ct)
                     ?? throw new ReglaNegocioException("NO_ENCONTRADO", "El evento no existe.");
        if (evento.Inicio <= _reloj.GetUtcNow().UtcDateTime)
            throw new ReglaNegocioException("EVENTO_INICIADO", ReglasEvento.MensajeInicioPasado);
        if (evento.Estado == Evento.EstadoCancelado)
            throw new ReglaNegocioException("YA_CANCELADO", "El evento ya está cancelado.");

        evento.Estado = Evento.EstadoCancelado;
        evento.Motivo = null;
        evento.UpdatedAt = _reloj.GetUtcNow().UtcDateTime;
        await _repository.GuardarCambiosAsync(evento, null, null, ct);

        var actualizado = (await _repository.ListarMiosAsync(usuarioId, ct))
            .First(e => e.Evento.Id == evento.Id);
        return ADto(actualizado);
    }

    public async Task<FotoEvento> ObtenerImagenAsync(int eventoId, int usuarioId, bool esAdmin, CancellationToken ct = default)
    {
        var foto = await _repository.ObtenerImagenAsync(eventoId, ct)
                   ?? throw new ReglaNegocioException("NO_ENCONTRADO", "El evento no tiene foto.");
        if (!esAdmin && foto.CreadorId != usuarioId && foto.Estado != Evento.EstadoPublicado)
            throw new ReglaNegocioException("NO_ENCONTRADO", "El evento no tiene foto.");
        return foto;
    }

    public async Task<List<EventoDto>> ListarPendientesAsync(CancellationToken ct = default)
        => (await _repository.ListarPendientesAsync(ct))
            .Select(ADto)
            .ToList();

    public async Task ModerarAsync(int eventoId, ResolverEventoRequest request, CancellationToken ct = default)
    {
        var estado = request.Estado?.Trim().ToUpperInvariant();
        if (estado is not (Evento.EstadoPublicado or Evento.EstadoRechazado))
            throw new ReglaNegocioException("VALIDACION", "Elige aprobar o rechazar el evento.");

        var motivo = ReglasSeguridadIa.Sanear(request.Motivo ?? "").Trim();
        if (estado == Evento.EstadoRechazado && string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("VALIDACION", "Escribe el motivo del rechazo.");
        if (motivo.Length > 500) throw new ReglaNegocioException("VALIDACION", "El motivo no puede superar 500 caracteres.");
        if (estado == Evento.EstadoPublicado) motivo = "";

        var resultado = await _repository.ModerarAsync(
            eventoId, estado, string.IsNullOrEmpty(motivo) ? null : motivo, _reloj.GetUtcNow().UtcDateTime, ct);
        if (resultado == ResultadoModeracionEvento.NoEncontrado)
            throw new ReglaNegocioException("NO_ENCONTRADO", "El evento no existe.");
        if (resultado == ResultadoModeracionEvento.YaRevisado)
            throw new ReglaNegocioException("YA_REVISADO", ReglasEvento.MensajeEventoNoPendiente);
    }

    private async Task<Lugar?> ResolverLugarAsync(int? lugarId, string? lugarTexto, CancellationToken ct)
    {
        if (lugarId is null) return null;
        var lugar = await _repository.ObtenerLugarAsync(lugarId.Value, ct);
        if (lugar is null && string.IsNullOrWhiteSpace(lugarTexto))
            throw new ReglaNegocioException("VALIDACION", "El lugar ya no está disponible. Escribe su nombre para enviarlo a revisión.");
        return lugar;
    }

    private static (string Titulo, string? Descripcion, string? Lugar, string Asistencia, string? Error)
        NormalizarYValidar(EventoFormularioRequest request)
    {
        var titulo = ReglasSeguridadIa.Sanear(request.Titulo ?? "").Trim();
        var descripcion = string.IsNullOrWhiteSpace(request.Descripcion)
            ? null
            : ReglasSeguridadIa.Sanear(request.Descripcion).Trim();
        var lugarTexto = string.IsNullOrWhiteSpace(request.LugarTexto)
            ? null
            : ReglasSeguridadIa.Sanear(request.LugarTexto).Trim();
        var asistencia = request.Asistencia?.Trim().ToUpperInvariant() ?? "";
        var error = ReglasEvento.Validar(titulo, descripcion, lugarTexto, request.LugarId,
            request.Inicio, request.Fin, asistencia, request.VisitaMin, request.VisitaMax);
        return (titulo, descripcion, lugarTexto, asistencia, error);
    }

    private static (string? TipoMime, string? Error) ValidarImagen(byte[]? imagen)
        => imagen is null ? (null, null) : ReglasEvento.ValidarImagen(imagen);

    private static EventoDto ADto(EventoListado item)
    {
        var evento = item.Evento;
        var offsetBolivia = TimeSpan.FromHours(-4);
        static DateTimeOffset Local(DateTime utc, TimeSpan offset)
            => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(offset);

        return new EventoDto(
            evento.Id,
            evento.Titulo,
            evento.Descripcion,
            Local(evento.Inicio, offsetBolivia),
            Local(evento.Fin, offsetBolivia),
            evento.LugarId,
            item.NombreLugar,
            evento.Estado,
            evento.Origen,
            evento.Asistencia,
            evento.VisitaMin,
            evento.VisitaMax,
            evento.Motivo,
            ReglasEvento.UrlImagen(evento.Id, item.FotoActualizadaEn),
            Local(evento.CreatedAt, offsetBolivia));
    }
}
