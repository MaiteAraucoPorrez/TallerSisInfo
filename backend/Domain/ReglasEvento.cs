using Breaku.Domain.Rules;

namespace Breaku.Domain;

public static class ReglasEvento
{
    public const int MaximoEventosPorDia = 3;
    public const int MaximoTitulo = 120;
    public const int MaximoDescripcion = 4_000;
    public const int MaximoLugar = 150;
    public const int MaximoVisitaMinutos = 825;
    public const int BytesMaximosImagen = 2 * 1024 * 1024;
    public const string AsistenciaFija = "FIJA";
    public const string AsistenciaLibre = "LIBRE";
    public const string MensajeContenidoPendiente = "El contenido requiere revisión de administración antes de publicarse.";
    public const string MensajeLugarPendiente = "El lugar no figura en el catálogo y requiere revisión de administración.";
    public const string MensajeFechaPendiente = "La fecha de inicio ya pasó; el evento requiere revisión de administración.";
    public const string MensajeHorarioPendiente = "El horario está fuera del horario del campus (07:15 a 21:00) y requiere revisión.";
    public const string MensajeLimiteDiario = "Ya alcanzaste el máximo de 3 eventos creados por día.";
    public const string MensajeInicioPasado = "No se puede editar ni cancelar un evento que ya empezó.";
    public const string MensajeEventoNoPendiente = "El evento ya fue revisado.";
    public const string MensajeOrigenInvalido = "El origen del evento no es válido.";
    public const string MensajeEventoNoEditable = "Solo se pueden editar los eventos pendientes o publicados.";
    public const string MensajeEventoFinalizado = "El evento ya terminó, así que no se puede revisar.";

    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);

    public static string? Validar(
        string? titulo,
        string? descripcion,
        string? lugarTexto,
        int? lugarId,
        DateTimeOffset? inicio,
        DateTimeOffset? fin,
        string? asistencia,
        int? visitaMin,
        int? visitaMax)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return "El título es obligatorio.";
        if (titulo.Trim().Length > MaximoTitulo) return $"El título no puede superar {MaximoTitulo} caracteres.";
        if (descripcion?.Length > MaximoDescripcion) return $"La descripción no puede superar {MaximoDescripcion} caracteres.";
        if (lugarId is <= 0) return "Selecciona un lugar válido del catálogo.";
        if (string.IsNullOrWhiteSpace(lugarTexto) && lugarId is null) return "Selecciona un lugar o escribe dónde será el evento.";
        if (lugarTexto?.Trim().Length > MaximoLugar) return $"El lugar no puede superar {MaximoLugar} caracteres.";
        if (inicio is null) return "La fecha y hora de inicio son obligatorias.";
        if (fin is null) return "La fecha y hora de fin son obligatorias.";
        if (fin <= inicio) return "La fecha y hora de fin deben ser posteriores al inicio.";
        if (asistencia is not (AsistenciaFija or AsistenciaLibre)) return "Indica si hay que llegar a la hora o si la visita es libre.";

        if (asistencia == AsistenciaFija && (visitaMin is not null || visitaMax is not null))
            return "Los tiempos de visita solo se usan cuando la asistencia es libre.";

        if (asistencia == AsistenciaLibre)
        {
            if (visitaMin is null || visitaMax is null)
                return "Indica el tiempo mínimo y máximo de visita.";
            if (visitaMin < 1 || visitaMax < visitaMin || visitaMax > MaximoVisitaMinutos)
                return $"El tiempo de visita debe estar entre 1 y {MaximoVisitaMinutos} minutos y el máximo no puede ser menor al mínimo.";
        }

        return null;
    }

    /// <summary>
    /// Origen con el que se guarda un evento nuevo. El cliente solo puede pedir FORMULARIO o FLYER (vacío = FORMULARIO);
    /// cualquier otro valor, incluido ADMIN, es un error. Un administrador siempre crea con origen ADMIN.
    /// </summary>
    public static (string? Origen, string? Error) ResolverOrigen(bool esAdmin, string? solicitado)
    {
        var pedido = string.IsNullOrWhiteSpace(solicitado) ? Evento.OrigenFormulario : solicitado.Trim().ToUpperInvariant();
        if (pedido is not (Evento.OrigenFormulario or Evento.OrigenFlyer))
            return (null, MensajeOrigenInvalido);
        return (esAdmin ? Evento.OrigenAdmin : pedido, null);
    }

    public static ResultadoPublicacion Evaluar(
        bool esAdmin,
        bool lugarCatalogado,
        string titulo,
        string? descripcion,
        string? lugarTexto,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        DateTimeOffset ahora)
    {
        if (esAdmin) return new ResultadoPublicacion(Evento.EstadoPublicado, null);

        var motivos = new List<string>();
        if (inicio <= ahora) motivos.Add(MensajeFechaPendiente);

        var inicioLocal = inicio.ToOffset(DesfaseBolivia);
        var finLocal = fin.ToOffset(DesfaseBolivia);
        if (inicioLocal.Date != finLocal.Date
            || inicioLocal.TimeOfDay < ReglasHorario.InicioMasTemprano.ToTimeSpan()
            || finLocal.TimeOfDay > ReglasHorario.FinMasTarde.ToTimeSpan())
            motivos.Add(MensajeHorarioPendiente);

        if (!lugarCatalogado) motivos.Add(MensajeLugarPendiente);
        var textoCompleto = string.Join(" ", new[] { titulo, descripcion, lugarTexto }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (ReglasSeguridadIa.Evaluar(textoCompleto) is not null)
            motivos.Add(MensajeContenidoPendiente);

        return motivos.Count == 0
            ? new ResultadoPublicacion(Evento.EstadoPublicado, null)
            : new ResultadoPublicacion(Evento.EstadoPendiente, string.Join(" ", motivos));
    }

    public static (string? TipoMime, string? Error) ValidarImagen(ReadOnlySpan<byte> contenido)
    {
        if (contenido.Length == 0) return (null, "La foto seleccionada está vacía.");
        if (contenido.Length > BytesMaximosImagen) return (null, "La foto no puede superar los 2 MB.");

        if (contenido.Length >= 3 && contenido[0] == 0xFF && contenido[1] == 0xD8 && contenido[2] == 0xFF)
            return ("image/jpeg", null);
        if (contenido.Length >= 8 && contenido[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ("image/png", null);

        return (null, "La foto del evento debe ser JPG o PNG.");
    }

    public static DateTime VersionImagen(DateTime ahoraUtc)
        => new(ahoraUtc.Ticks - ahoraUtc.Ticks % 10, DateTimeKind.Utc);

    public static string? UrlImagen(int eventoId, DateTime? actualizadaEn)
        => actualizadaEn is { } fecha ? $"/api/eventos/{eventoId}/foto?v={fecha.Ticks}" : null;

    public static (DateTime DesdeUtc, DateTime HastaUtc) RangoDiaLocal(DateTimeOffset ahora)
    {
        var local = ahora.ToOffset(DesfaseBolivia);
        var inicioLocal = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, DesfaseBolivia);
        return (inicioLocal.UtcDateTime, inicioLocal.AddDays(1).UtcDateTime);
    }

    public sealed record ResultadoPublicacion(string Estado, string? Motivo);
}
