namespace Breaku.Domain;

public sealed class Evento
{
    public const string EstadoPendiente = "PENDIENTE";
    public const string EstadoPublicado = "PUBLICADO";
    public const string EstadoRechazado = "RECHAZADO";
    public const string EstadoCancelado = "CANCELADO";

    public const string OrigenFormulario = "FORMULARIO";
    public const string OrigenFlyer = "FLYER";
    public const string OrigenAdmin = "ADMIN";

    public int Id { get; set; }
    public int CreadorId { get; set; }
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public int? LugarId { get; set; }
    public Lugar? Lugar { get; set; }
    public string? LugarTexto { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public string Estado { get; set; } = EstadoPendiente;
    public string Origen { get; set; } = OrigenFormulario;
    public string Asistencia { get; set; } = "";
    public short? VisitaMin { get; set; }
    public short? VisitaMax { get; set; }
    public string? Motivo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public EventoImagen? Imagen { get; set; }
}
