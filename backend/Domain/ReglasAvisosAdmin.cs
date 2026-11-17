using Breaku.Domain.Rules;

namespace Breaku.Domain;

/// <summary>
/// Avisos de la campanita del administrador: qué le llega y con qué texto (hasta 200 caracteres, como cualquier aviso).
/// Le llega a cada admin activo cuando hay algo que necesita su decisión, y queda leído para todos cuando uno lo resuelve
/// (como en GitHub o en Slack: si otro ya se ocupó, no queda como pendiente).
/// </summary>
public static class ReglasAvisosAdmin
{
    public const int LargoMaximo = 200;

    public static string MensajeReporteNuevo(string motivo, string? descripcion)
    {
        var base_ = $"Nuevo reporte «{ReglasReporte.EtiquetaMotivo(motivo)}».";
        return ConCita(base_, descripcion);
    }

    public static string MensajeEventoPorRevisar(string titulo, string? motivo)
    {
        var base_ = $"«{Recortar(titulo.Trim(), 80)}» espera tu revisión.";
        return ConCita(base_, motivo);
    }

    /// <summary>Agrega el texto entre comillas, recortado para que el aviso quepa en 200 caracteres.</summary>
    private static string ConCita(string base_, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return base_;
        const string apertura = " «";
        const string cierre = "»";
        var espacio = LargoMaximo - base_.Length - apertura.Length - cierre.Length;
        if (espacio < 10) return base_;
        return base_ + apertura + Recortar(texto.Trim().ReplaceLineEndings(" "), espacio) + cierre;
    }

    private static string Recortar(string texto, int maximo)
        => texto.Length <= maximo ? texto : texto[..(maximo - 1)].TrimEnd() + "…";
}
