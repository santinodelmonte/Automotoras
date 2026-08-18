using AutomotoraSaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutomotoraSaaS.Infrastructure.Persistence.Configurations;

public sealed class PrecioReferenciaConfiguration : IEntityTypeConfiguration<PrecioReferencia>
{
    public void Configure(EntityTypeBuilder<PrecioReferencia> builder)
    {
        builder.ToTable("precios_referencia");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Fuente).HasMaxLength(40).IsRequired();

        builder.Property(p => p.Promedio).HasPrecision(12, 2);
        builder.Property(p => p.Minimo).HasPrecision(12, 2);
        builder.Property(p => p.Maximo).HasPrecision(12, 2);

        // Un snapshot por modelo, año, moneda, fecha y fuente. Es lo que hace que el job
        // pueda reintentar sin duplicar la serie.
        builder.HasIndex(p => new { p.ModeloId, p.Anio, p.Moneda, p.Fecha, p.Fuente }).IsUnique();

        // La consulta que importa es "el último precio de este modelo y año".
        builder.HasIndex(p => new { p.ModeloId, p.Anio, p.Fecha });

        builder.HasOne(p => p.Modelo)
            .WithMany()
            .HasForeignKey(p => p.ModeloId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
