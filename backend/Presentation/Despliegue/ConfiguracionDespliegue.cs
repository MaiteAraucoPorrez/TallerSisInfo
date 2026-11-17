namespace Breaku.Presentation.Despliegue;

/// <summary>
/// Lo que cambia cuando el backend corre en internet (Render) y no solo en la compu: varios frontends permitidos
/// (localhost y Vercel) y saber la IP real de quien llama cuando hay un proxy adelante.
/// </summary>
public static class ConfiguracionDespliegue
{
    /// <summary>
    /// FRONTEND_ORIGIN acepta uno o varios orígenes separados por coma o punto y coma, por ejemplo
    /// "http://localhost:3000,https://breaku.vercel.app". Sin barra final y sin repetidos.
    /// </summary>
    public static string[] Origenes(string? valor) =>
        (valor ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'))
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// Detrás del proxy de Render todas las llamadas llegan con la IP del proxy: sin leer X-Forwarded-For, el límite de
    /// intentos de login (por IP) sería uno solo para todos. Render define RENDER=true; en otro servicio, DETRAS_DE_PROXY=true.
    /// </summary>
    public static bool DetrasDeProxy(string? render, string? detrasDeProxy) =>
        EsVerdadero(render) || EsVerdadero(detrasDeProxy);

    private static bool EsVerdadero(string? valor) =>
        string.Equals(valor?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
