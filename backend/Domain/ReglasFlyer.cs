using System.Globalization;
using System.Text.RegularExpressions;

namespace Breaku.Domain.Rules;

/// <summary>Lo que el lector (LLM) dice haber visto en el flyer o en el texto. Todo es una sugerencia: nada se confía.</summary>
public sealed record EventoLeido(
    bool Entendido,
    bool Legible,
    string? Titulo,
    string? Descripcion,
    string? Inicio,
    string? Fin,
    string? Lugar,
    string? Asistencia,
    int? VisitaMinMin,
    int? VisitaMaxMin,
    bool VariosEventos,
    IReadOnlyList<string> Advertencias
);

public sealed record BorradorEvento(
    string? Titulo,
    string? Descripcion,
    DateTime? Inicio,
    DateTime? Fin,
    string? LugarTexto,
    string? Asistencia,
    int? VisitaMin,
    int? VisitaMax
);

public sealed record ResultadoFlyer(
    string Estado,
    BorradorEvento Evento,
    IReadOnlyList<string> Faltantes,
    IReadOnlyList<string> Advertencias,
    string? Mensaje
);

/// <summary>HU24: convierte la lectura de un flyer en un borrador de evento. HU23 decide la publicación.</summary>
public static class ReglasFlyer
{
    public const string AsistenciaFija = "FIJA";
    public const string AsistenciaLibre = "LIBRE";

    public const string CampoTitulo = "titulo";
    public const string CampoInicio = "inicio";
    public const string CampoFin = "fin";
    public const string CampoLugar = "lugar";

    public const int MaximoLargoTitulo = 120;
    public const int MaximoLargoDescripcion = 1000;
    public const int MaximoLargoLugar = 150;
    public const int MaximoLargoTextoPegado = 4000;
    public const int MaximoAdvertencias = 5;
    public const int MaximoLargoAdvertencia = 160;

    /// <summary>Un evento de esta duración o más se sugiere LIBRE (se puede pasar un rato); menos, FIJO.</summary>
    public const int DuracionLibreMin = 180;
    public const int VisitaMinDefectoMin = 15;
    public const int VisitaMaxDefectoMin = 30;
    public const int VisitaMaximaPermitidaMin = 240;

    public const string MensajeNoEsEvento = "No encontramos un evento en lo que enviaste. Revisa el archivo o llena el formulario a mano.";
    public const string MensajeIlegible = "La imagen se ve borrosa; revisa los datos o súbela con más calidad";
    public const string MensajeFaltan = "No pudimos leer todos los datos del evento. Completa los que faltan.";
    public const string MensajeFechaPasada = "La fecha del evento ya pasó. Indica una fecha futura.";
    public const string AvisoVariosEventos = "El afiche tiene más de un evento; tomamos el principal.";
    public const string AvisoContenidoDescartado = "Se descartó un dato del afiche que no pudimos aceptar.";

    private static readonly string[] FormatosFecha =
        ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"];

    public static DateTime? LeerFecha(string? texto)
        => DateTime.TryParseExact(texto?.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    public static ResultadoFlyer Interpretar(EventoLeido? leido, DateTime ahoraLocal)
    {
        var vacio = new BorradorEvento(null, null, null, null, null, null, null, null);
        if (leido is null || (!leido.Entendido && leido.Legible))
            return new ResultadoFlyer(ReglasExtraccion.EstadoNoEntendido, vacio, [], [], MensajeNoEsEvento);

        var advertencias = new List<string>();
        var descartado = false;

        string? Limpio(string? valor, int maximo, int minimoLetras)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;
            if (ReglasSeguridadIa.EsManipulacion(valor)) { descartado = true; return null; }
            var t = Regex.Replace(ReglasSeguridadIa.Sanear(valor), @"\s+", " ").Trim();
            if (t.Count(char.IsLetterOrDigit) < minimoLetras) return null;
            return t.Length > maximo ? t[..maximo].TrimEnd() : t;
        }

        var titulo = Limpio(leido.Titulo, MaximoLargoTitulo, 2);
        var descripcion = Limpio(leido.Descripcion, MaximoLargoDescripcion, 1);
        var lugar = Limpio(leido.Lugar, MaximoLargoLugar, 2);

        var inicio = LeerFecha(leido.Inicio);
        var fin = LeerFecha(leido.Fin);
        var fechaPasada = false;
        if (inicio is { } i && i < ahoraLocal) { inicio = null; fechaPasada = true; }
        if (fin is { } f && f <= ahoraLocal) { fin = null; fechaPasada = true; }
        if (inicio is { } i2 && fin is { } f2 && f2 <= i2) fin = null;

        var asistencia = ElegirAsistencia(leido.Asistencia, inicio, fin);
        int? visitaMin = null, visitaMax = null;
        if (asistencia == AsistenciaLibre) (visitaMin, visitaMax) = ElegirVisita(leido.VisitaMinMin, leido.VisitaMaxMin, inicio, fin);

        foreach (var a in leido.Advertencias.Take(MaximoAdvertencias))
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            if (ReglasSeguridadIa.EsManipulacion(a)) { descartado = true; continue; }
            var t = Regex.Replace(ReglasSeguridadIa.Sanear(a), @"\s+", " ").Trim();
            advertencias.Add(t.Length > MaximoLargoAdvertencia ? t[..MaximoLargoAdvertencia].TrimEnd() : t);
        }
        if (leido.VariosEventos) advertencias.Add(AvisoVariosEventos);
        if (descartado) advertencias.Add(AvisoContenidoDescartado);

        var faltantes = new List<string>();
        if (titulo is null) faltantes.Add(CampoTitulo);
        if (inicio is null) faltantes.Add(CampoInicio);
        if (fin is null) faltantes.Add(CampoFin);
        if (lugar is null) faltantes.Add(CampoLugar);

        var datos = new BorradorEvento(titulo, descripcion, inicio, fin, lugar, asistencia, visitaMin, visitaMax);
        if (!leido.Legible)
            return new ResultadoFlyer(ReglasExtraccion.EstadoIncompleto, datos, faltantes, advertencias, MensajeIlegible);
        if (faltantes.Count == 0)
            return new ResultadoFlyer(ReglasExtraccion.EstadoCompleto, datos, [], advertencias, null);

        var mensaje = fechaPasada && (faltantes.Contains(CampoInicio) || faltantes.Contains(CampoFin)) ? MensajeFechaPasada : MensajeFaltan;
        return new ResultadoFlyer(ReglasExtraccion.EstadoIncompleto, datos, faltantes, advertencias, mensaje);
    }

    /// <summary>El LLM sugiere; si no dice nada válido, la regla de respaldo: 3 h o más → LIBRE, si no FIJA.</summary>
    public static string ElegirAsistencia(string? sugerida, DateTime? inicio, DateTime? fin)
    {
        var s = sugerida?.Trim().ToUpperInvariant();
        if (s is AsistenciaFija or AsistenciaLibre) return s;
        return inicio is { } i && fin is { } f && (f - i).TotalMinutes >= DuracionLibreMin ? AsistenciaLibre : AsistenciaFija;
    }

    /// <summary>Visita corta sugerida. Valores inválidos del modelo se reemplazan por 15–30 min, acotados a la duración.</summary>
    public static (int Min, int Max) ElegirVisita(int? minimo, int? maximo, DateTime? inicio, DateTime? fin)
    {
        int? duracion = inicio is { } i && fin is { } f ? (int)(f - i).TotalMinutes : null;
        var tope = Math.Min(VisitaMaximaPermitidaMin, duracion ?? VisitaMaximaPermitidaMin);

        if (minimo is > 0 && maximo is > 0 && minimo <= maximo && maximo <= tope) return (minimo.Value, maximo.Value);

        var max = Math.Min(VisitaMaxDefectoMin, tope);
        var min = Math.Min(VisitaMinDefectoMin, max);
        return (Math.Max(min, 1), Math.Max(max, 1));
    }
}
