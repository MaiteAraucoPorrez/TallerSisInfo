using Breaku.Application.Lugares;
using Breaku.Application.Puentes;
using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Application.Eventos;

public sealed class EventosEnPuenteService
{
    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);
    private readonly IEventoRepository _eventos;
    private readonly ILugarRepository _lugares;
    private readonly IPuenteRepository _puentes;
    private readonly TimeProvider _reloj;

    public EventosEnPuenteService(
        IEventoRepository eventos,
        ILugarRepository lugares,
        IPuenteRepository puentes) : this(eventos, lugares, puentes, TimeProvider.System) { }

    public EventosEnPuenteService(
        IEventoRepository eventos,
        ILugarRepository lugares,
        IPuenteRepository puentes,
        TimeProvider reloj)
    {
        _eventos = eventos;
        _lugares = lugares;
        _puentes = puentes;
        _reloj = reloj;
    }

    public async Task<List<EventoEnPuenteDto>> ListarAsync(int usuarioId, CancellationToken ct = default)
    {
        var ahoraUtc = _reloj.GetUtcNow();
        var ahoraLocal = ahoraUtc.ToOffset(DesfaseBolivia);
        var minutoActual = ahoraLocal.Hour * 60 + ahoraLocal.Minute;
        var diaHoy = (short)ahoraLocal.DayOfWeek;

        var puentesHoy = (await _puentes.ListarPorUsuarioAsync(usuarioId, ct))
            .Where(p => p.DiaSemana == diaHoy && (int)p.HoraFin.ToTimeSpan().TotalMinutes > minutoActual)
            .ToList();
        if (puentesHoy.Count == 0) return [];

        var zona = await _lugares.ObtenerZonaDelUsuarioAsync(usuarioId, ct);
        if (zona is null) return [];

        var (desdeUtc, hastaUtc) = ReglasEvento.RangoDiaLocal(ahoraUtc);
        var eventos = await _eventos.ListarPublicadosHoyAsync(desdeUtc, hastaUtc, ahoraUtc.UtcDateTime, ct);
        var recomendaciones = new List<EventoEnPuenteDto>();

        foreach (var candidato in eventos)
        {
            var distancia = (int)Math.Round(ReglasBusquedaLugares.DistanciaMetros(
                zona.Latitud, zona.Longitud, candidato.Lugar.Latitud, candidato.Lugar.Longitud));
            var traslado = ReglasBusquedaLugares.TiempoTrasladoIdaVueltaMin(distancia);
            var mejor = puentesHoy
                .Select(p => ReglasEventos.EvaluarEnPuente(candidato.Evento, p, ahoraUtc, traslado))
                .Where(r => r is not null)
                .OrderByDescending(r => r!.MinutosPuedeQuedarse ?? 0)
                .FirstOrDefault();
            if (mejor is null) continue;

            recomendaciones.Add(new EventoEnPuenteDto(
                candidato.Evento.Id,
                candidato.Evento.Titulo,
                HoraLocal(candidato.Evento.Inicio),
                HoraLocal(candidato.Evento.Fin),
                candidato.Lugar.Nombre,
                candidato.Evento.Asistencia,
                mejor.MinutosPuedeQuedarse,
                mejor.Explicacion));
        }

        return recomendaciones
            .OrderBy(e => e.Inicio, StringComparer.Ordinal)
            .ThenBy(e => e.Titulo, StringComparer.CurrentCulture)
            .ToList();
    }

    private static string HoraLocal(DateTime utc)
        => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))
            .ToOffset(DesfaseBolivia)
            .ToString("HH:mm");
}
