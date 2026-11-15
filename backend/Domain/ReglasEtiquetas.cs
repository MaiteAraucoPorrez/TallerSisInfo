using System.Text.RegularExpressions;

namespace Breaku.Domain.Rules;

/// <summary>
/// HU30: el catálogo de etiquetas de tipo de actividad (<c>etiqueta_actividad</c>) que gestiona el admin, y su asignación a
/// los lugares (<c>lugar_etiqueta</c>). Un lugar ACTIVO y recomendable tiene que tener al menos una: sin eso la
/// personalización no sabe qué tipo de actividad es.
/// </summary>
public static class ReglasEtiquetas
{
    public const int LargoMaximoNombre = 50;   // etiqueta_actividad.nombre es VARCHAR(50)

    public const string MensajeNombre = "El nombre de la etiqueta tiene que tener entre 1 y 50 caracteres.";
    public const string MensajeRepetida = "Ya existe una etiqueta con ese nombre.";
    public const string MensajeNoExiste = "Una de las etiquetas elegidas no existe en el catálogo.";
    public const string MensajeSinEtiqueta =
        "Elige al menos un tipo de actividad: sin eso el lugar no se puede activar ni recomendar.";

    /// <summary>Sin espacios de más: «  Juegos   de mesa » → «Juegos de mesa».</summary>
    public static string LimpiarNombre(string? nombre)
        => Regex.Replace(nombre ?? "", @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();

    public static string? ValidarNombre(string nombreLimpio)
        => nombreLimpio.Length is 0 or > LargoMaximoNombre ? MensajeNombre : null;

    /// <summary>Los ids sin repetir y en orden, o <c>null</c> si no vinieron (el lugar conserva las que tenía).</summary>
    public static List<int>? Normalizar(IEnumerable<int>? etiquetaIds) => etiquetaIds?.Distinct().Order().ToList();

    /// <summary>
    /// El primer problema de las etiquetas elegidas, o <c>null</c> si están bien: todas tienen que existir en el catálogo y,
    /// si el lugar queda activo y recomendable, tiene que haber al menos una.
    /// </summary>
    public static string? ValidarAsignacion(IReadOnlyCollection<int> etiquetaIds, IReadOnlyCollection<int> catalogo, bool quedaActivoYRecomendable)
    {
        if (etiquetaIds.Any(id => !catalogo.Contains(id))) return MensajeNoExiste;
        if (quedaActivoYRecomendable && etiquetaIds.Count == 0) return MensajeSinEtiqueta;
        return null;
    }

    /// <summary>Si se puede pasar el lugar a ACTIVO: los de referencia (no recomendables) no necesitan etiqueta.</summary>
    public static bool PuedeActivarse(bool recomendable, int cantidadEtiquetas) => !recomendable || cantidadEtiquetas > 0;
}
