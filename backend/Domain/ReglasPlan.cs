namespace Breaku.Domain.Rules;

/// <summary>
/// Planes con tus conexiones de «Libres contigo» (migración 032): armar un plan para los dos en el rato libre que
/// comparten hoy, o invitar a la conexión a una comunidad tuya o a un evento de hoy que caiga en ese rato.
/// Reglas puras: las horas son de Bolivia y los días van de 1 (lunes) a 6 (sábado), como en los puentes.
/// </summary>
public static class ReglasPlan
{
    /// <summary>Un plan es para los dos: con el cupo justo, nadie más ocupa el lugar.</summary>
    public const short ParticipantesPlan = 2;

    /// <summary>Si del rato en común quedan menos minutos que esto, ya no se propone un plan.</summary>
    public const int MinutosMinimosRestantes = 15;

    /// <summary>Lo mismo que permite una comunidad (<c>ck_com_duracion</c>).</summary>
    public const int MaximoMinutos = 240;

    public const int MaximoLargoActividad = 100;
    public const int MaximoLargoAviso = 200;

    public const string ErrorSinTramo = "SIN_TRAMO";
    public const string MensajeSinTramo = "Hoy ya no les queda un rato libre en común. Prueben otro día.";
    public const string ErrorLugarNoViable = "LUGAR_NO_VIABLE";
    public const string MensajeLugarNoViable = "Ese lugar no está entre las opciones para ese rato. Elige otro.";
    public const string ErrorFueraDelTramo = "FUERA_DEL_TRAMO";
    public const string MensajeFueraDelTramo = "Eso no cae en el rato libre que comparten hoy.";
    public const string ErrorYaInvitado = "YA_INVITADO";
    public const string MensajeYaInvitado = "Ya está invitado (o ya va) a esa comunidad.";
    public const string ErrorCompleta = "COMPLETA";
    public const string MensajeCompleta = "Esa comunidad ya está completa.";
    public const string ErrorCruce = "CRUCE";
    public const string MensajePrivada = "Es un plan con una conexión: para sumar a alguien, invítalo desde «Libres contigo».";

    public readonly record struct Tramo(int MiPuenteId, TimeOnly Inicio, TimeOnly Fin)
    {
        public int Minutos => (int)(Fin.ToTimeSpan() - Inicio.ToTimeSpan()).TotalMinutes;
    }

    /// <summary>
    /// Los ratos libres que los dos comparten HOY y todavía no terminaron (30 min o más en común, como el matching),
    /// recortados a «desde ahora» (redondeado a los 5 minutos) y a 4 horas como máximo. Ordenados por hora.
    /// </summary>
    public static List<Tramo> TramosDeHoy(IEnumerable<(int Id, ReglasMatching.Franja Franja)> misPuentes,
        IEnumerable<ReglasMatching.Franja> susPuentes, int diaHoy, TimeOnly ahora)
    {
        var suyos = susPuentes.Where(f => f.DiaSemana == diaHoy).ToList();
        var desde = RedondearArriba(ahora, 5);
        var tramos = new List<Tramo>();

        foreach (var (id, mia) in misPuentes.Where(p => p.Franja.DiaSemana == diaHoy))
        foreach (var suya in suyos)
        {
            if (ReglasMatching.Cruce(mia, suya) is not { } enComun) continue;
            var inicio = enComun.Inicio > desde ? enComun.Inicio : desde;
            if ((enComun.Fin.ToTimeSpan() - inicio.ToTimeSpan()).TotalMinutes < MinutosMinimosRestantes) continue;
            var tope = inicio.AddMinutes(MaximoMinutos);
            var fin = enComun.Fin < tope || tope < inicio ? enComun.Fin : tope;
            tramos.Add(new Tramo(id, inicio, fin));
        }

        return tramos
            .DistinctBy(t => (t.MiPuenteId, t.Inicio))
            .OrderBy(t => t.Inicio)
            .ThenBy(t => t.MiPuenteId)
            .ToList();
    }

    /// <summary>
    /// Saca de los ratos lo que alguno de los dos ya tiene comprometido (comunidades creadas o confirmadas): un rato puede
    /// partirse en dos. Quedan solo los pedazos de <see cref="MinutosMinimosRestantes"/> minutos o más.
    /// </summary>
    public static List<Tramo> QuitarOcupados(IEnumerable<Tramo> tramos, IReadOnlyCollection<(TimeOnly Inicio, TimeOnly Fin)> ocupados)
    {
        var libres = new List<Tramo>();
        foreach (var tramo in tramos)
        {
            var pedazos = new List<Tramo> { tramo };
            foreach (var (oIni, oFin) in ocupados.OrderBy(o => o.Inicio))
                pedazos = pedazos.SelectMany(p => Restar(p, oIni, oFin)).ToList();
            libres.AddRange(pedazos.Where(p => p.Minutos >= MinutosMinimosRestantes));
        }
        return libres.OrderBy(t => t.Inicio).ThenBy(t => t.MiPuenteId).ToList();
    }

    private static IEnumerable<Tramo> Restar(Tramo t, TimeOnly oIni, TimeOnly oFin)
    {
        if (oFin <= t.Inicio || oIni >= t.Fin) { yield return t; yield break; }
        if (oIni > t.Inicio) yield return t with { Fin = oIni };
        if (oFin < t.Fin) yield return t with { Inicio = oFin };
    }

    /// <summary>10:31 → 10:35 con paso 5. Si ya está en el paso, queda igual. No pasa de 23:55.</summary>
    public static TimeOnly RedondearArriba(TimeOnly hora, int paso)
    {
        var minutos = hora.Hour * 60 + hora.Minute + (hora.Second > 0 || hora.Millisecond > 0 ? 1 : 0);
        var redondeado = (minutos + paso - 1) / paso * paso;
        return redondeado >= 24 * 60 ? new TimeOnly(23, 55) : new TimeOnly(redondeado / 60, redondeado % 60);
    }

    /// <summary>¿Se pisan [inicio, fin) y el tramo? (basta con un minuto en común).</summary>
    public static bool SeSuperpone(TimeOnly inicio, TimeOnly fin, Tramo tramo) => inicio < tramo.Fin && fin > tramo.Inicio;

    public static bool CaeEnAlgunTramo(TimeOnly inicio, TimeOnly fin, IEnumerable<Tramo> tramos)
        => tramos.Any(t => SeSuperpone(inicio, fin, t));

    public static string ActividadDelPlan(string lugar) => Recortar($"Plan en {lugar.Trim()}", MaximoLargoActividad);

    /// <summary>
    /// Nunca vacío: AsistenciaService usa un texto original vacío para reconocer a un organizador heredado (HU28).
    /// </summary>
    public static string TextoOriginal(string nombreConexion) => $"Plan con {nombreConexion.Trim()} desde Libres contigo";

    public static string MensajePlan(string quien, string lugar, TimeOnly inicio, TimeOnly fin)
        => Aviso(l => $"{quien} te propone un plan: {l}, hoy de {Hora(inicio)} a {Hora(fin)}. ¿Vas?", lugar);

    public static string MensajeInvitacionComunidad(string quien, string actividad, TimeOnly inicio, TimeOnly fin)
        => Aviso(a => $"{quien} te invita a «{a}», hoy de {Hora(inicio)} a {Hora(fin)}.", actividad);

    public static string MensajeInvitacionEvento(string quien, string titulo, TimeOnly inicio, TimeOnly fin)
        => Aviso(t => $"{quien} te invita al evento «{t}», hoy de {Hora(inicio)} a {Hora(fin)}.", titulo);

    public static string MensajeCruce(string actividad) => $"A esa hora ya vas a «{actividad}».";

    private static string Hora(TimeOnly t) => t.ToString("HH:mm");

    /// <summary>Arma el aviso y, si pasa de 200 caracteres, recorta solo el nombre variable (con «…»).</summary>
    private static string Aviso(Func<string, string> armar, string variable)
    {
        var mensaje = armar(variable);
        if (mensaje.Length <= MaximoLargoAviso) return mensaje;
        var sobra = mensaje.Length - MaximoLargoAviso + 1;
        return armar(variable[..Math.Max(1, variable.Length - sobra)].TrimEnd() + "…");
    }

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..(maximo - 1)].TrimEnd() + "…";
}
