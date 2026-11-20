using Breaku.Application.Eventos;
using Breaku.Application.Horarios;
using Breaku.Application.Lugares;
using Breaku.Application.Puentes;
using Breaku.Application.TiempoReal;
using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Application.Matching;

public record TramoPlanDto(int PuenteId, string HoraInicio, string HoraFin, int Minutos);

public record ComunidadInvitableDto(int Id, string Actividad, string HoraInicio, string HoraFin, int Confirmados, int MaxParticipantes);

public record EventoInvitableDto(int Id, string Titulo, string Lugar, string HoraInicio, string HoraFin);

/// <summary>Lo que se puede hacer hoy con una conexión: armar un plan en uno de los ratos que comparten o invitarla a algo.</summary>
public record OpcionesPlanDto(int ConexionId, string Nombre, List<TramoPlanDto> Tramos, List<ComunidadInvitableDto> Comunidades,
    List<EventoInvitableDto> Eventos);

public record CrearPlanRequest(int? PuenteId, int? LugarId);

public record PlanCreadoDto(int ComunidadId, string Actividad, string HoraInicio, string HoraFin, string Mensaje);

/// <summary>Una de las dos: <c>comunidadId</c> (una comunidad tuya) o <c>eventoId</c> (un evento de hoy).</summary>
public record InvitarConexionRequest(int? ComunidadId, int? EventoId);

public record InvitacionEnviadaDto(string Mensaje);

/// <summary>Una comunidad activa que creaste, con cuántos van y quiénes ya están adentro.</summary>
public record ComunidadMia(ComunidadEfimera Comunidad, int Confirmados, IReadOnlyCollection<int> Participantes);

public interface IPlanesRepository
{
    /// <summary>Tus comunidades activas que todavía no terminaron y empiezan antes de <paramref name="hastaUtc"/>.</summary>
    Task<List<ComunidadMia>> ListarMisComunidadesAsync(int usuarioId, DateTime ahoraUtc, DateTime hastaUtc, CancellationToken ct = default);

    /// <summary>La actividad de otra comunidad activa (creada o confirmada) que se cruza con ese horario, o null.</summary>
    Task<string?> CruceAsync(int usuarioId, DateTime inicioUtc, DateTime finUtc, CancellationToken ct = default);

    /// <summary>Horarios (UTC) de las comunidades activas que estos usuarios crearon o confirmaron entre esas horas.</summary>
    Task<List<(DateTime InicioUtc, DateTime FinUtc)>> OcupadosAsync(IReadOnlyCollection<int> usuarioIds, DateTime desdeUtc, DateTime hastaUtc,
        CancellationToken ct = default);

    Task<bool> QuiereAvisosAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>Crea el plan (privado), con quien lo arma confirmada y la conexión invitada (con su aviso, si lo hay).</summary>
    Task<int> CrearPlanAsync(ComunidadEfimera plan, int invitadoId, string? aviso, DateTime ahoraUtc, CancellationToken ct = default);

    /// <summary>Invita a la comunidad. False si ya estaba adentro (no se duplica).</summary>
    Task<bool> InvitarAComunidadAsync(int comunidadId, int invitadoId, string? aviso, DateTime ahoraUtc, CancellationToken ct = default);

    /// <summary>Deja (o renueva) el aviso INVITACION_EVENTO para ese evento.</summary>
    Task InvitarAEventoAsync(int eventoId, int invitadoId, string aviso, DateTime ahoraUtc, CancellationToken ct = default);
}

/// <summary>
/// «Armar plan» e «Invitar a…» con una conexión aceptada de «Libres contigo». Todo es de HOY y dentro del rato libre que
/// los dos comparten, recalculado con sus puentes actuales (si alguien cambió su horario, se respeta el nuevo).
/// </summary>
public class PlanesService
{
    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);

    private readonly IConexionRepository _conexiones;
    private readonly IPuenteRepository _puentes;
    private readonly IEventoRepository _eventos;
    private readonly IPlanesRepository _repo;
    private readonly BusquedaLugaresService _lugares;
    private readonly ITiempoReal _tiempoReal;
    private readonly TimeProvider _reloj;

    public PlanesService(IConexionRepository conexiones, IPuenteRepository puentes, IEventoRepository eventos, IPlanesRepository repo,
        BusquedaLugaresService lugares, ITiempoReal tiempoReal)
        : this(conexiones, puentes, eventos, repo, lugares, tiempoReal, TimeProvider.System) { }

    public PlanesService(IConexionRepository conexiones, IPuenteRepository puentes, IEventoRepository eventos, IPlanesRepository repo,
        BusquedaLugaresService lugares, ITiempoReal tiempoReal, TimeProvider reloj)
    {
        _conexiones = conexiones;
        _puentes = puentes;
        _eventos = eventos;
        _repo = repo;
        _lugares = lugares;
        _tiempoReal = tiempoReal;
        _reloj = reloj;
    }

    public async Task<OpcionesPlanDto> OpcionesAsync(int usuarioId, int conexionId, CancellationToken ct = default)
    {
        var c = await ContextoAsync(usuarioId, conexionId, ct);
        var comunidades = (await _repo.ListarMisComunidadesAsync(usuarioId, c.AhoraUtc, c.FinDelDiaUtc, ct))
            .Where(m => Invitable(m, c.OtroId) && ReglasPlan.CaeEnAlgunTramo(Local(m.Comunidad.Inicio), Local(m.Comunidad.Fin), c.Tramos))
            .Select(m => new ComunidadInvitableDto(m.Comunidad.Id, m.Comunidad.Actividad, Hora(Local(m.Comunidad.Inicio)),
                Hora(Local(m.Comunidad.Fin)), m.Confirmados, m.Comunidad.MaxParticipantes))
            .ToList();
        var eventos = (await EventosDeHoyAsync(c, ct))
            .Where(e => ReglasPlan.CaeEnAlgunTramo(Local(e.Evento.Inicio), Local(e.Evento.Fin), c.Tramos))
            .Select(e => new EventoInvitableDto(e.Evento.Id, e.Evento.Titulo, e.Lugar.Nombre, Hora(Local(e.Evento.Inicio)), Hora(Local(e.Evento.Fin))))
            .ToList();

        return new OpcionesPlanDto(conexionId, c.NombreOtro,
            c.Tramos.Select(t => new TramoPlanDto(t.MiPuenteId, Hora(t.Inicio), Hora(t.Fin), t.Minutos)).ToList(),
            comunidades, eventos);
    }

    public async Task<PlanCreadoDto> CrearPlanAsync(int usuarioId, int conexionId, CrearPlanRequest req, CancellationToken ct = default)
    {
        if (req.PuenteId is not int puenteId || puenteId <= 0 || req.LugarId is not int lugarId || lugarId <= 0)
            throw new ReglaNegocioException("VALIDACION", "Elige el rato (puenteId) y el lugar (lugarId) del plan.");

        var c = await ContextoAsync(usuarioId, conexionId, ct);
        var tramo = c.Tramos.FirstOrDefault(t => t.MiPuenteId == puenteId);
        if (tramo == default) throw new ReglaNegocioException(ReglasPlan.ErrorSinTramo, ReglasPlan.MensajeSinTramo);

        // El lugar tiene que ser una de las opciones reales para ese rato (catálogo activo, dentro del radio y que quepa).
        var viables = await _lugares.BuscarParaPuenteAsync(usuarioId, puenteId, ct);
        var lugar = viables.Lugares.FirstOrDefault(l => l.LugarId == lugarId)
                    ?? throw new ReglaNegocioException(ReglasPlan.ErrorLugarNoViable, ReglasPlan.MensajeLugarNoViable);

        var inicioUtc = Utc(c.Hoy, tramo.Inicio);
        var finUtc = Utc(c.Hoy, tramo.Fin);
        if (await _repo.CruceAsync(usuarioId, inicioUtc, finUtc, ct) is { } cruce)
            throw new ReglaNegocioException(ReglasPlan.ErrorCruce, ReglasPlan.MensajeCruce(cruce));

        var plan = new ComunidadEfimera
        {
            CreadorId = usuarioId,
            TextoOriginal = ReglasPlan.TextoOriginal(c.NombreOtro),
            Actividad = ReglasPlan.ActividadDelPlan(lugar.Nombre),
            Inicio = inicioUtc,
            Fin = finUtc,
            MaxParticipantes = ReglasPlan.ParticipantesPlan,
            LugarId = lugar.LugarId,
            Privada = true,
            Estado = ComunidadEfimera.EstadoActiva,
            CreatedAt = c.AhoraUtc,
            UpdatedAt = c.AhoraUtc,
        };
        var aviso = await _repo.QuiereAvisosAsync(c.OtroId, ct)
            ? ReglasPlan.MensajePlan(c.MiNombreCorto, lugar.Nombre, tramo.Inicio, tramo.Fin)
            : null;
        var id = await _repo.CrearPlanAsync(plan, c.OtroId, aviso, c.AhoraUtc, ct);
        await _tiempoReal.AvisosCambiaronAsync([c.OtroId], ct);

        return new PlanCreadoDto(id, plan.Actividad, Hora(tramo.Inicio), Hora(tramo.Fin),
            $"Listo: le propusiste a {c.NombreOtro} ir a {lugar.Nombre} de {Hora(tramo.Inicio)} a {Hora(tramo.Fin)}.");
    }

    public async Task<InvitacionEnviadaDto> InvitarAsync(int usuarioId, int conexionId, InvitarConexionRequest req, CancellationToken ct = default)
    {
        var aComunidad = req.ComunidadId is int cid && cid > 0;
        var aEvento = req.EventoId is int eid && eid > 0;
        if (aComunidad == aEvento)
            throw new ReglaNegocioException("VALIDACION", "Indica una comunidad (comunidadId) o un evento (eventoId), no los dos.");

        var c = await ContextoAsync(usuarioId, conexionId, ct);
        return aComunidad
            ? await InvitarAComunidadAsync(c, req.ComunidadId!.Value, ct)
            : await InvitarAEventoAsync(c, req.EventoId!.Value, ct);
    }

    private async Task<InvitacionEnviadaDto> InvitarAComunidadAsync(Contexto c, int comunidadId, CancellationToken ct)
    {
        var mia = (await _repo.ListarMisComunidadesAsync(c.UsuarioId, c.AhoraUtc, c.FinDelDiaUtc, ct))
                  .FirstOrDefault(m => m.Comunidad.Id == comunidadId)
                  ?? throw new ReglaNegocioException("NO_ENCONTRADO", "La comunidad no existe o ya terminó.");
        var (inicio, fin) = (Local(mia.Comunidad.Inicio), Local(mia.Comunidad.Fin));
        if (!ReglasPlan.CaeEnAlgunTramo(inicio, fin, c.Tramos))
            throw new ReglaNegocioException(ReglasPlan.ErrorFueraDelTramo, ReglasPlan.MensajeFueraDelTramo);
        if (mia.Participantes.Contains(c.OtroId))
            throw new ReglaNegocioException(ReglasPlan.ErrorYaInvitado, ReglasPlan.MensajeYaInvitado);
        if (mia.Confirmados >= mia.Comunidad.MaxParticipantes)
            throw new ReglaNegocioException(ReglasPlan.ErrorCompleta, ReglasPlan.MensajeCompleta);

        var aviso = await _repo.QuiereAvisosAsync(c.OtroId, ct)
            ? ReglasPlan.MensajeInvitacionComunidad(c.MiNombreCorto, mia.Comunidad.Actividad, inicio, fin)
            : null;
        if (!await _repo.InvitarAComunidadAsync(comunidadId, c.OtroId, aviso, c.AhoraUtc, ct))
            throw new ReglaNegocioException(ReglasPlan.ErrorYaInvitado, ReglasPlan.MensajeYaInvitado);
        await _tiempoReal.AvisosCambiaronAsync([c.OtroId], ct);
        return new InvitacionEnviadaDto($"Listo: invitaste a {c.NombreOtro} a «{mia.Comunidad.Actividad}».");
    }

    private async Task<InvitacionEnviadaDto> InvitarAEventoAsync(Contexto c, int eventoId, CancellationToken ct)
    {
        var evento = (await EventosDeHoyAsync(c, ct)).FirstOrDefault(e => e.Evento.Id == eventoId)
                     ?? throw new ReglaNegocioException("NO_ENCONTRADO", "El evento no existe, no es de hoy o ya terminó.");
        var (inicio, fin) = (Local(evento.Evento.Inicio), Local(evento.Evento.Fin));
        if (!ReglasPlan.CaeEnAlgunTramo(inicio, fin, c.Tramos))
            throw new ReglaNegocioException(ReglasPlan.ErrorFueraDelTramo, ReglasPlan.MensajeFueraDelTramo);

        // El aviso es lo único que lleva la invitación: si tiene los avisos apagados, se le avisa igual porque lo pidió una conexión.
        await _repo.InvitarAEventoAsync(eventoId, c.OtroId,
            ReglasPlan.MensajeInvitacionEvento(c.MiNombreCorto, evento.Evento.Titulo, inicio, fin), c.AhoraUtc, ct);
        await _tiempoReal.AvisosCambiaronAsync([c.OtroId], ct);
        return new InvitacionEnviadaDto($"Listo: invitaste a {c.NombreOtro} a «{evento.Evento.Titulo}».");
    }

    private sealed record Contexto(int UsuarioId, int OtroId, string NombreOtro, string MiNombreCorto, DateTime AhoraUtc,
        DateTime FinDelDiaUtc, DateOnly Hoy, List<ReglasPlan.Tramo> Tramos);

    /// <summary>Valida que la conexión sea tuya y esté aceptada, y calcula los ratos que comparten hoy.</summary>
    private async Task<Contexto> ContextoAsync(int usuarioId, int conexionId, CancellationToken ct)
    {
        var conexion = await _conexiones.ObtenerAsync(conexionId, ct);
        if (conexion is null || conexion.Estado != ReglasConexion.Aceptada
            || (conexion.SolicitanteId != usuarioId && conexion.DestinatarioId != usuarioId))
            throw new ReglaNegocioException("NO_ENCONTRADO", "La conexión no existe.");

        var otroId = conexion.SolicitanteId == usuarioId ? conexion.DestinatarioId : conexion.SolicitanteId;
        var personas = await _conexiones.ObtenerPersonasAsync([usuarioId, otroId], ct);
        if (!personas.TryGetValue(otroId, out var otro))
            throw new ReglaNegocioException("NO_ENCONTRADO", "La conexión no existe.");
        var yo = personas.TryGetValue(usuarioId, out var p) ? p.Nombre : "";

        var ahora = _reloj.GetUtcNow();
        var local = ahora.ToOffset(DesfaseBolivia);
        var hoy = DateOnly.FromDateTime(local.DateTime);
        var (_, finDelDia) = ReglasEvento.RangoDiaLocal(ahora);

        var mios = (await _puentes.ListarPorUsuarioAsync(usuarioId, ct)).Select(x => (x.Id, Franja(x)));
        var suyos = (await _puentes.ListarPorUsuarioAsync(otroId, ct)).Select(Franja);
        // Los ratos que comparten, menos lo que cualquiera de los dos ya tiene comprometido: así el plan no le choca a nadie.
        var ocupados = (await _repo.OcupadosAsync([usuarioId, otroId], ahora.UtcDateTime, finDelDia, ct))
            .Select(o => (Local(o.InicioUtc), Local(o.FinUtc))).ToList();
        var tramos = ReglasPlan.QuitarOcupados(
            ReglasPlan.TramosDeHoy(mios, suyos, (int)local.DayOfWeek, TimeOnly.FromDateTime(local.DateTime)), ocupados);

        return new Contexto(usuarioId, otroId, otro.Nombre, ReglasDescubrir.NombreCorto(yo), ahora.UtcDateTime, finDelDia, hoy, tramos);
    }

    private async Task<List<EventoParaPuente>> EventosDeHoyAsync(Contexto c, CancellationToken ct)
    {
        var (desde, hasta) = ReglasEvento.RangoDiaLocal(new DateTimeOffset(c.AhoraUtc, TimeSpan.Zero));
        return await _eventos.ListarPublicadosHoyAsync(desde, hasta, c.AhoraUtc, ct);
    }

    private static bool Invitable(ComunidadMia m, int otroId)
        => !m.Participantes.Contains(otroId) && m.Confirmados < m.Comunidad.MaxParticipantes;

    private static ReglasMatching.Franja Franja(Puente p) => new(p.DiaSemana, p.HoraInicio, p.HoraFin);

    private static TimeOnly Local(DateTime utc) => TimeOnly.FromDateTime(utc + DesfaseBolivia);

    private static DateTime Utc(DateOnly dia, TimeOnly hora) => DateTime.SpecifyKind(dia.ToDateTime(hora) - DesfaseBolivia, DateTimeKind.Utc);

    private static string Hora(TimeOnly t) => t.ToString("HH:mm");
}
