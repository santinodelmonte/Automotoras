using AutomotoraSaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutomotoraSaaS.Infrastructure.Persistence.Configurations;

public sealed class DominioDeTenantConfiguration : IEntityTypeConfiguration<DominioDeTenant>
{
    public void Configure(EntityTypeBuilder<DominioDeTenant> builder)
    {
        builder.ToTable("dominios_tenant");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Dominio).HasMaxLength(255).IsRequired();
        builder.Property(d => d.TokenDeVerificacion).HasMaxLength(64).IsRequired();
        builder.Property(d => d.UltimoError).HasMaxLength(500);

        // Único en todo el sistema y no por tenant: un dominio resuelve a un solo sitio en
        // internet, y dos filas iguales harían indeterminado el tenant que se resuelve.
        builder.HasIndex(d => d.Dominio).IsUnique();

        // La resolución del sitio público entra por acá en cada request anónimo.
        builder.HasIndex(d => new { d.Estado, d.Dominio });

        builder.HasOne(d => d.Tenant)
            .WithMany(t => t.Dominios)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
