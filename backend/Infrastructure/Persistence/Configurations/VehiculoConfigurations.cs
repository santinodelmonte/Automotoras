using AutomotoraSaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutomotoraSaaS.Infrastructure.Persistence.Configurations;

public sealed class VehiculoConfiguration : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> builder)
    {
        builder.ToTable("vehiculos");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Color).HasMaxLength(40);
        builder.Property(v => v.Motor).HasMaxLength(60);
        builder.Property(v => v.Descripcion).HasMaxLength(4000);

        // decimal(12,2) cubre precios en pesos uruguayos sin desbordar y sin perder
        // centavos. Nunca float: el redondeo binario no es aceptable en un precio.
        builder.Property(v => v.Precio).HasPrecision(12, 2);
        builder.Property(v => v.PrecioCosto).HasPrecision(12, 2);
        builder.Property(v => v.PrecioVenta).HasPrecision(12, 2);

        // Índices obligatorios del brief.
        // El primero sostiene el listado público, que siempre filtra por tenant y estado.
        builder.HasIndex(v => new { v.TenantId, v.Estado });
        // El segundo sostiene la analítica cross-tenant de fase 2: qué modelo y año se
        // mueve en el mercado.
        builder.HasIndex(v => new { v.ModeloId, v.Anio });

        builder.HasOne(v => v.Tenant)
            .WithMany(t => t.Vehiculos)
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Modelo)
            .WithMany()
            .HasForeignKey(v => v.ModeloId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Version)
            .WithMany()
            .HasForeignKey(v => v.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VehiculoFotoConfiguration : IEntityTypeConfiguration<VehiculoFoto>
{
    public void Configure(EntityTypeBuilder<VehiculoFoto> builder)
    {
        builder.ToTable("vehiculo_fotos");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Url).HasMaxLength(500).IsRequired();
        builder.Property(f => f.UrlThumb).HasMaxLength(500);

        builder.HasIndex(f => new { f.VehiculoId, f.Orden });

        builder.HasOne(f => f.Vehiculo)
            .WithMany(v => v.Fotos)
            .HasForeignKey(f => f.VehiculoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
