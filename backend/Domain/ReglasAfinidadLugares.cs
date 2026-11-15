namespace Breaku.Domain.Rules;

/// <summary>
/// HU30: recomendaciones según los «me gusta» y «no me sirve» anteriores del propio estudiante. La personalización es por
/// usuario y por tipo de actividad (las etiquetas del catálogo en <c>lugar_etiqueta</c>), no un ranking global: si dos
/// estudiantes piden lo mismo, al que antes le gustó lo deportivo le suben los lugares deportivos, y al otro, lo suyo.
/// <para>
/// Solo reordena lugares que ya pasaron los filtros de siempre (estado, radio, métricas y <c>CabeEnElPuente</c>): nunca agrega
/// uno que no cabe. Puntúa todos los candidatos antes del recorte a 10, así la afinidad decide cuáles llegan al LLM.
/// </para>
/// <para>
/// Comparte nombre con las reglas del test de afinidad de HU12 (es una clase <c>partial</c>): el issue pide llamarlas
/// <c>ReglasAfinidad</c>, y las dos hablan de afinidad del estudiante.
/// </para>
/// </summary>
public static partial class ReglasAfinidad
{
    public const string MeGusta = "LIKE";
    public const string NoMeSirve = "DISLIKE";

    /// <summary>Valores de arranque (se ajustan con datos reales): un «me gusta» suma 1 a sus etiquetas, un «no me sirve» resta 2.</summary>
    public const int PesoMeGusta = 1;
    public const int PesoNoMeSirve = -2;

    /// <summary>Un lugar con «no me sirve» no se vuelve a recomendar durante estos días.</summary>
    public const int DiasSinRepetirNoMeSirve = 30;

    /// <summary>Feedbacks TOTALES del estudiante (no por etiqueta) para empezar a personalizar. Por debajo, todo queda como antes.</summary>
    public const int MinimoFeedbacks = 3;

    /// <summary>Cuántas etiquetas de cada lado van en el resumen para el LLM.</summary>
    public const int MaximoEtiquetasEnResumen = 3;

    /// <summary>Un voto del estudiante sobre un lugar recomendado, con las etiquetas que tenía ese lugar.</summary>
    public sealed record VotoLugar(int LugarId, string Feedback, DateTime FechaUtc, IReadOnlyCollection<int> Etiquetas);

    /// <summary>Un lugar que ya pasó los filtros (cabe en el puente) y hay que ordenar.</summary>
    public sealed record CandidatoAfinidad(int LugarId, string Nombre, int DistanciaM, IReadOnlyCollection<int> Etiquetas);

    /// <summary>
    /// El historial resumido: si personaliza, el peso de cada etiqueta y los lugares que no se repiten. Sin historial
    /// suficiente, <see cref="Personaliza"/> es false y nada cambia.
    /// </summary>
    public sealed record Perfil(bool Personaliza, IReadOnlyDictionary<int, int> PesosPorEtiqueta, IReadOnlySet<int> LugaresVetados)
    {
        public static readonly Perfil Vacio = new(false, new Dictionary<int, int>(), new HashSet<int>());
    }

    /// <summary>Arma el perfil con el historial del estudiante (solo el suyo). <paramref name="ahoraUtc"/> fija la ventana de 30 días.</summary>
    public static Perfil ArmarPerfil(IEnumerable<VotoLugar> votos, DateTime ahoraUtc)
    {
        var validos = votos.Where(v => v.Feedback is MeGusta or NoMeSirve).ToList();
        if (validos.Count < MinimoFeedbacks) return Perfil.Vacio;

        var pesos = new Dictionary<int, int>();
        foreach (var voto in validos)
            foreach (var etiqueta in voto.Etiquetas.Distinct())
                pesos[etiqueta] = pesos.GetValueOrDefault(etiqueta) + (voto.Feedback == MeGusta ? PesoMeGusta : PesoNoMeSirve);

        var desde = ahoraUtc.AddDays(-DiasSinRepetirNoMeSirve);
        var vetados = validos.Where(v => v.Feedback == NoMeSirve && v.FechaUtc >= desde).Select(v => v.LugarId).ToHashSet();

        return new Perfil(true, pesos, vetados);
    }

    /// <summary>Lo afín que es un lugar: la suma de los pesos de sus etiquetas (0 si no tiene o no hay historial).</summary>
    public static int Puntaje(IReadOnlyCollection<int> etiquetasDelLugar, Perfil perfil)
        => perfil.Personaliza ? etiquetasDelLugar.Distinct().Sum(e => perfil.PesosPorEtiqueta.GetValueOrDefault(e)) : 0;

    /// <summary>
    /// Ordena los candidatos (que ya caben en el puente). Sin personalización: como siempre, por distancia y nombre. Con
    /// personalización: saca los vetados por «no me sirve» y ordena por puntaje (de mayor a menor), con desempate
    /// determinista por distancia, nombre e id; sin popularidad global.
    /// </summary>
    public static List<T> Ordenar<T>(IEnumerable<T> candidatos, Func<T, CandidatoAfinidad> datos, Perfil perfil)
    {
        var lista = candidatos.Select(c => (Item: c, Datos: datos(c)));
        // Sin historial suficiente, exactamente el orden de antes de HU30 (el mismo comparador de nombres).
        if (!perfil.Personaliza)
            return lista.OrderBy(x => x.Datos.DistanciaM).ThenBy(x => x.Datos.Nombre).Select(x => x.Item).ToList();

        return lista
            .Where(x => !perfil.LugaresVetados.Contains(x.Datos.LugarId))
            .OrderByDescending(x => Puntaje(x.Datos.Etiquetas, perfil))
            .ThenBy(x => x.Datos.DistanciaM)
            .ThenBy(x => x.Datos.Nombre, StringComparer.Ordinal)
            .ThenBy(x => x.Datos.LugarId)
            .Select(x => x.Item)
            .ToList();
    }

    /// <summary>Lo que se le cuenta al LLM: solo nombres de etiquetas, nunca votos, lugares ni datos del estudiante.</summary>
    public sealed record ResumenPreferencias(IReadOnlyList<string> LeGustan, IReadOnlyList<string> PrefiereEvitar);

    /// <summary>
    /// Las etiquetas con peso positivo (le gustan) y negativo (prefiere evitar), de la más marcada a la menos, hasta 3 de cada
    /// lado. <c>null</c> si no personaliza o si no hay nada que contar.
    /// </summary>
    public static ResumenPreferencias? Resumir(Perfil perfil, IReadOnlyDictionary<int, string> nombresEtiquetas)
    {
        if (!perfil.Personaliza) return null;

        List<string> Nombres(IEnumerable<KeyValuePair<int, int>> pesos) => pesos
            .Where(p => nombresEtiquetas.ContainsKey(p.Key))
            .Select(p => (Nombre: nombresEtiquetas[p.Key], Peso: Math.Abs(p.Value)))
            .OrderByDescending(p => p.Peso).ThenBy(p => p.Nombre, StringComparer.Ordinal)
            .Take(MaximoEtiquetasEnResumen)
            .Select(p => p.Nombre)
            .ToList();

        var gustan = Nombres(perfil.PesosPorEtiqueta.Where(p => p.Value > 0));
        var evitar = Nombres(perfil.PesosPorEtiqueta.Where(p => p.Value < 0));
        return gustan.Count == 0 && evitar.Count == 0 ? null : new ResumenPreferencias(gustan, evitar);
    }
}
