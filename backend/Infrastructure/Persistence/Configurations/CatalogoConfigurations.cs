using AutomotoraSaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutomotoraSaaS.Infrastructure.Persistence.Configurations;

public sealed class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> builder)
    {
        builder.ToTable("marcas");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Nombre).HasMaxLength(80).IsRequired();

        // La unicidad es lo que hace que la normalización sirva de algo: sin esto vuelven
        // "VW" y "Volkswagen" como dos marcas distintas y la agregación se rompe.
        builder.HasIndex(m => m.Nombre).IsUnique();
    }
}

public sealed class ModeloConfiguration : IEntityTypeConfiguration<Modelo>
{
    public void Configure(EntityTypeBuilder<Modelo> builder)
    {
        builder.ToTable("modelos");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Nombre).HasMaxLength(80).IsRequired();

        builder.HasIndex(m => new { m.MarcaId, m.Nombre }).IsUnique();

        builder.HasOne(m => m.Marca)
            .WithMany(ma => ma.Modelos)
            .HasForeignKey(m => m.MarcaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VersionVehiculoConfiguration : IEntityTypeConfiguration<VersionVehiculo>
{
    public void Configure(EntityTypeBuilder<VersionVehiculo> builder)
    {
        builder.ToTable("versiones");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Nombre).HasMaxLength(80).IsRequired();

        builder.HasIndex(v => new { v.ModeloId, v.Nombre }).IsUnique();

        builder.HasOne(v => v.Modelo)
            .WithMany(m => m.Versiones)
            .HasForeignKey(v => v.ModeloId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SolicitudModeloConfiguration : IEntityTypeConfiguration<SolicitudModelo>
{
    public void Configure(EntityTypeBuilder<SolicitudModelo> builder)
    {
        builder.ToTable("solicitudes_modelo");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.NombreModelo).HasMaxLength(80).IsRequired();
        builder.Property(s => s.NotaResolucion).HasMaxLength(500);

        builder.HasIndex(s => new { s.TenantId, s.Estado });

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.SolicitadaPor)
            .WithMany()
            .HasForeignKey(s => s.SolicitadaPorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Marca)
            .WithMany()
            .HasForeignKey(s => s.MarcaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ModeloCreado)
            .WithMany()
            .HasForeignKey(s => s.ModeloCreadoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
