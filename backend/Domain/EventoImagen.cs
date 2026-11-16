namespace Breaku.Domain;

public sealed class EventoImagen
{
    public int EventoId { get; set; }
    public Evento Evento { get; set; } = null!;
    public byte[] Contenido { get; set; } = [];
    public string TipoMime { get; set; } = "";
    public int Bytes { get; set; }
    public DateTime ActualizadaEn { get; set; }
}
