using Breaku.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Breaku.Infrastructure.Persistence;

public sealed class EventoConfiguration : IEntityTypeConfiguration<Evento>
{
    public void Configure(EntityTypeBuilder<Evento> builder)
    {
        builder.ToTable("evento");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreadorId).HasColumnName("creador_id");
        builder.Property(e => e.Titulo).HasColumnName("titulo").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Descripcion).HasColumnName("descripcion").HasColumnType("text");
        builder.Property(e => e.LugarId).HasColumnName("lugar_id");
        builder.Property(e => e.LugarTexto).HasColumnName("lugar_texto").HasMaxLength(150);
        builder.Property(e => e.Inicio).HasColumnName("inicio").HasColumnType("datetime(6)");
        builder.Property(e => e.Fin).HasColumnName("fin").HasColumnType("datetime(6)");
        builder.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Origen).HasColumnName("origen").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Asistencia).HasColumnName("asistencia").HasMaxLength(5).IsRequired();
        builder.Property(e => e.VisitaMin).HasColumnName("visita_min");
        builder.Property(e => e.VisitaMax).HasColumnName("visita_max");
        builder.Property(e => e.Motivo).HasColumnName("motivo").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");

        builder.HasIndex(e => new { e.Estado, e.Fin }).HasDatabaseName("ix_evento_estado_fin");
        builder.HasIndex(e => new { e.CreadorId, e.CreatedAt }).HasDatabaseName("ix_evento_creador");
        builder.HasOne<Usuario>().WithMany().HasForeignKey(e => e.CreadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Lugar).WithMany().HasForeignKey(e => e.LugarId).OnDelete(DeleteBehavior.Restrict);
    }
}
