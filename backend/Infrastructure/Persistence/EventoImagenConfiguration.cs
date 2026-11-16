using Breaku.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Breaku.Infrastructure.Persistence;

public sealed class EventoImagenConfiguration : IEntityTypeConfiguration<EventoImagen>
{
    public void Configure(EntityTypeBuilder<EventoImagen> builder)
    {
        builder.ToTable("evento_imagen");
        builder.HasKey(i => i.EventoId);

        builder.Property(i => i.EventoId).HasColumnName("evento_id");
        builder.Property(i => i.Contenido).HasColumnName("contenido").HasColumnType("mediumblob").IsRequired();
        builder.Property(i => i.TipoMime).HasColumnName("tipo_mime").HasMaxLength(20).IsRequired();
        builder.Property(i => i.Bytes).HasColumnName("bytes");
        builder.Property(i => i.ActualizadaEn).HasColumnName("actualizada_en").HasColumnType("datetime(6)");

        builder.HasOne(i => i.Evento).WithOne(e => e.Imagen)
            .HasForeignKey<EventoImagen>(i => i.EventoId).OnDelete(DeleteBehavior.Cascade);
    }
}
