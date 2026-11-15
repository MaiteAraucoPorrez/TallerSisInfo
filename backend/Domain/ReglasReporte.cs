
namespace Breaku.Domain.Rules;

public static class ReglasReporte
{
    public const string Pendiente = "PENDIENTE";
    public const string Resuelto = "RESUELTO";
    public const string Descartado = "DESCARTADO";

    public const string MensajeDecisionInvalida = "El estado tiene que ser RESUELTO o DESCARTADO.";
    public const string MensajeNoExiste = "El reporte no existe.";
    public const string MensajeYaRevisado = "Este reporte ya fue revisado.";

    public static string? LeerDecision(string? estado)
    {
        var limpio = estado?.Trim().ToUpperInvariant();
        return limpio is Resuelto or Descartado ? limpio : null;
    }

    public static bool SePuedeRevisar(string estadoActual) => estadoActual == Pendiente;

    // ---------- Reportes que envía el estudiante ----------

    public const string MotivoErrorApp = "ERROR_APP";
    public const string MotivoSugerencia = "SUGERENCIA";
    public const string MotivoLugarIncorrecto = "LUGAR_INCORRECTO";
    public const string MotivoComunidadInapropiada = "COMUNIDAD_INAPROPIADA";
    public const string MotivoEventoInapropiado = "EVENTO_INAPROPIADO";

    /// <summary>Los motivos que puede enviar el estudiante: los generales desde el perfil, y la comunidad y el evento desde su pantalla.</summary>
    public static readonly string[] MotivosDelEstudiante =
        [MotivoErrorApp, MotivoSugerencia, MotivoLugarIncorrecto, MotivoComunidadInapropiada, MotivoEventoInapropiado];

    /// <summary>A qué apunta un reporte (lo mismo que exige <c>ck_rep_objeto</c> en la base).</summary>
    public const string ObjetoLugar = "LUGAR";
    public const string ObjetoComunidad = "COMUNIDAD";
    public const string ObjetoEvento = "EVENTO";

    public const int LargoMinimoDescripcion = 10;
    public const int LargoMaximoDescripcion = 500;
    public const int MaximoPendientesPorEstudiante = 5;
    public const int LargoMaximoRespuesta = 500;
    /// <summary>Lo que entra en <c>notificacion.mensaje</c> (VARCHAR(200)).</summary>
    public const int LargoMaximoAviso = 200;

    public const string MensajeMotivo = "Elige el tipo de reporte.";
    public const string MensajeFaltaLugar = "Elige el lugar que tiene la información incorrecta.";
    public const string MensajeFaltaComunidad = "Falta la comunidad que quieres reportar.";
    public const string MensajeFaltaEvento = "Falta el evento que quieres reportar.";
    public const string MensajeObjetoQueNoCorresponde =
        "Este tipo de reporte no va con ese objeto: un lugar va con «Información incorrecta», una comunidad con «Comunidad inapropiada» y un evento con «Evento inapropiado».";
    public const string MensajeDescripcionCorta = "Cuéntanos un poco más: al menos 10 caracteres.";
    public const string MensajeDescripcionLarga = "Puedes escribir hasta 500 caracteres.";
    public const string MensajeLugarNoExiste = "Ese lugar no existe.";
    public const string MensajeComunidadNoExiste = "Esa comunidad no existe.";
    public const string MensajeEventoNoExiste = "Ese evento no existe.";
    public const string MensajeYaReportado = "Ya reportaste este lugar y todavía lo estamos revisando.";
    public const string MensajeYaReportadaComunidad = "Ya reportaste esta comunidad y todavía la estamos revisando.";
    public const string MensajeYaReportadoEvento = "Ya reportaste este evento y todavía lo estamos revisando.";
    public const string MensajeDemasiados = "Ya tienes 5 reportes pendientes. Espera a que los revisemos antes de enviar otro.";
    public const string MensajeRespuestaLarga = "La respuesta puede tener hasta 500 caracteres.";

    /// <summary>El motivo en mayúsculas, o <c>null</c> si no es uno de los que puede enviar el estudiante.</summary>
    public static string? LeerMotivo(string? motivo)
    {
        var limpio = motivo?.Trim().ToUpperInvariant();
        return limpio is not null && MotivosDelEstudiante.Contains(limpio) ? limpio : null;
    }

    /// <summary>Qué objeto pide el motivo (<c>LUGAR</c>, <c>COMUNIDAD</c> o <c>EVENTO</c>), o <c>null</c> si no lleva ninguno.</summary>
    public static string? ObjetoDelMotivo(string motivo) => motivo switch
    {
        MotivoLugarIncorrecto => ObjetoLugar,
        MotivoComunidadInapropiada => ObjetoComunidad,
        MotivoEventoInapropiado => ObjetoEvento,
        _ => null
    };

    /// <summary>Reporte general o de un lugar (como antes de HU27): sin comunidad ni evento.</summary>
    public static string? ValidarNuevo(string? motivo, int? lugarId, string? descripcion)
        => ValidarNuevo(motivo, lugarId, null, null, descripcion);

    /// <summary>
    /// El primer problema del reporte, o <c>null</c> si se puede guardar. Cada motivo lleva solo su objeto: el lugar con
    /// LUGAR_INCORRECTO, la comunidad con COMUNIDAD_INAPROPIADA, el evento con EVENTO_INAPROPIADO; los generales, ninguno.
    /// </summary>
    public static string? ValidarNuevo(string? motivo, int? lugarId, int? comunidadId, int? eventoId, string? descripcion)
    {
        var m = LeerMotivo(motivo);
        if (m is null) return MensajeMotivo;

        var esperado = ObjetoDelMotivo(m);
        if (esperado == ObjetoLugar && lugarId is null or <= 0) return MensajeFaltaLugar;
        if (esperado == ObjetoComunidad && comunidadId is null or <= 0) return MensajeFaltaComunidad;
        if (esperado == ObjetoEvento && eventoId is null or <= 0) return MensajeFaltaEvento;
        if ((esperado != ObjetoLugar && lugarId is not null)
            || (esperado != ObjetoComunidad && comunidadId is not null)
            || (esperado != ObjetoEvento && eventoId is not null))
            return MensajeObjetoQueNoCorresponde;

        var largo = descripcion?.Trim().Length ?? 0;
        if (largo < LargoMinimoDescripcion) return MensajeDescripcionCorta;
        if (largo > LargoMaximoDescripcion) return MensajeDescripcionLarga;
        return null;
    }

    /// <summary>El mensaje de «ya reportaste esto» según el objeto.</summary>
    public static string MensajeYaReportadoDe(string objeto) => objeto switch
    {
        ObjetoComunidad => MensajeYaReportadaComunidad,
        ObjetoEvento => MensajeYaReportadoEvento,
        _ => MensajeYaReportado
    };

    // ---------- Respuesta del admin y aviso al estudiante ----------

    /// <summary>La respuesta sin espacios de más, o <c>null</c> si vino vacía (es opcional).</summary>
    public static string? LimpiarRespuesta(string? respuesta)
        => string.IsNullOrWhiteSpace(respuesta) ? null : respuesta.Trim();

    /// <summary>El problema de la respuesta ya limpia, o <c>null</c> si se puede guardar (hasta 500 caracteres).</summary>
    public static string? ValidarRespuesta(string? respuestaLimpia)
        => respuestaLimpia is not null && respuestaLimpia.Length > LargoMaximoRespuesta ? MensajeRespuestaLarga : null;

    /// <summary>Cómo se nombra el motivo ante el estudiante.</summary>
    public static string EtiquetaMotivo(string motivo) => motivo switch
    {
        MotivoErrorApp => "Algo no funciona",
        MotivoSugerencia => "Sugerencia",
        MotivoLugarIncorrecto => "Información de un lugar",
        MotivoComunidadInapropiada => "Comunidad inapropiada",
        MotivoEventoInapropiado => "Evento inapropiado",
        _ => "Reporte"
    };

    /// <summary>
    /// El texto del aviso REPORTE_RESUELTO: qué se decidió y, si el admin respondió, su respuesta recortada para que todo
    /// quepa en 200 caracteres (la respuesta completa se lee en «Mis reportes»).
    /// </summary>
    public static string MensajeAviso(string motivo, string decision, string? respuesta)
    {
        var verbo = decision == Descartado ? "descartado" : "resuelto";
        var base_ = $"Tu reporte «{EtiquetaMotivo(motivo)}» fue {verbo}.";
        if (string.IsNullOrWhiteSpace(respuesta)) return base_;

        const string prefijo = " Respuesta: «";
        const string cierre = "»";
        var espacio = LargoMaximoAviso - base_.Length - prefijo.Length - cierre.Length;
        var texto = respuesta.Trim().ReplaceLineEndings(" ");
        if (texto.Length > espacio) texto = texto[..(espacio - 1)].TrimEnd() + "…";
        return base_ + prefijo + texto + cierre;
    }
}
