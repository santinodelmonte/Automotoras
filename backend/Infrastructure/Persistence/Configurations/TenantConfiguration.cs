using AutomotoraSaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutomotoraSaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Slug).HasMaxLength(60).IsRequired();
        builder.Property(t => t.Nombre).HasMaxLength(160).IsRequired();
        builder.Property(t => t.DominioCustom).HasMaxLength(255);
        builder.Property(t => t.LogoUrl).HasMaxLength(500);
        builder.Property(t => t.ColorPrimario).HasMaxLength(7);
        builder.Property(t => t.ColorSecundario).HasMaxLength(7);
        builder.Property(t => t.Whatsapp).HasMaxLength(30);
        builder.Property(t => t.Telefono).HasMaxLength(30);
        builder.Property(t => t.Direccion).HasMaxLength(255);

        // El slug resuelve el tenant en /t/{slug} y el dominio lo resuelve por Host.
        // Los dos son claves de entrada al sitio público: si se duplican, el tenant que
        // se resuelve pasa a ser indeterminado.
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasIndex(t => t.DominioCustom).IsUnique();
    }
}
