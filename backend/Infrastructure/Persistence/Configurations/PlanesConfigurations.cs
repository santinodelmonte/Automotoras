using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutomotoraSaaS.Infrastructure.Persistence.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    /// <summary>Fecha de alta de los planes sembrados. Fija: <c>HasData</c> no admite valores que cambien.</summary>
    private static readonly DateTime AltaDelCatalogo = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("planes");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(80).IsRequired();
        builder.Property(p => p.PrecioMensual).HasPrecision(12, 2);

        builder.HasIndex(p => p.Codigo).IsUnique();

        // Los tres planes de la propuesta van como datos de la migración y no del seed de
        // desarrollo: en producción tienen que existir igual. Valores en pesos uruguayos,
        // sin IVA. De acá en más se editan desde el panel, no con otra migración.
        builder.HasData(
            new Plan
            {
                Id = 1,
                Codigo = CodigosDePlan.Vidriera,
                Nombre = "Vidriera",
                PrecioMensual = 4_000m,
                Moneda = Moneda.Uyu,
                MaxVehiculos = 40,
                MaxUsuarios = 2,
                IncluyeReportes = false,
                IncluyeBenchmark = false,
                IncluyeDominioPropio = false,
                HorasSoporteMes = 0,
                Activo = true,
                CreatedAt = AltaDelCatalogo,
            },
            new Plan
            {
                Id = 2,
                Codigo = CodigosDePlan.Demanda,
                Nombre = "Demanda",
                PrecioMensual = 6_900m,
                Moneda = Moneda.Uyu,
                MaxVehiculos = 120,
                MaxUsuarios = 6,
                IncluyeReportes = true,
                IncluyeBenchmark = false,
                IncluyeDominioPropio = true,
                HorasSoporteMes = 2,
                Activo = true,
                CreatedAt = AltaDelCatalogo,
            },
            new Plan
            {
                Id = 3,
                Codigo = CodigosDePlan.Full,
                Nombre = "Full",
                PrecioMensual = 9_900m,
                Moneda = Moneda.Uyu,
                MaxVehiculos = null,
                MaxUsuarios = null,
                IncluyeReportes = true,
                IncluyeBenchmark = true,
                IncluyeDominioPropio = true,
                HorasSoporteMes = 5,
                Activo = true,
                CreatedAt = AltaDelCatalogo,
            });
    }
}

public sealed class SuscripcionConfiguration : IEntityTypeConfiguration<Suscripcion>
{
    /// <summary>
    /// Columna generada: el tenant si la suscripción está vigente, nulo si ya cerró.
    /// </summary>
    /// <remarks>
    /// Es la forma de decir "una sola vigente por tenant" en MySQL, que no tiene índices
    /// parciales. Un índice único admite cualquier cantidad de nulos, así que las
    /// suscripciones cerradas no chocan entre sí y dos vigentes del mismo tenant sí.
    /// </remarks>
    public const string ColumnaTenantVigente = "TenantIdVigente";

    public void Configure(EntityTypeBuilder<Suscripcion> builder)
    {
        builder.ToTable("suscripciones");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.MotivoDeBaja).HasMaxLength(500);

        // Stored y no virtual: lo indexa igual MySQL, MariaDB y SQLite. El SQL es el
        // mismo en los tres.
        builder.Property<int?>(ColumnaTenantVigente)
            .HasComputedColumnSql("CASE WHEN fin IS NULL THEN tenant_id END", stored: true);

        builder.HasIndex(ColumnaTenantVigente).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Inicio });

        // Restrict y no Cascade, por dos motivos. El historial de cobranza no se borra
        // de rebote. Y MySQL no admite CASCADE en una FK sobre una columna de la que
        // depende una columna generada stored, como tenant_id acá.
        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AvisoDeCobroConfiguration : IEntityTypeConfiguration<AvisoDeCobro>
{
    public void Configure(EntityTypeBuilder<AvisoDeCobro> builder)
    {
        builder.ToTable("avisos_de_cobro");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Destinatarios).HasMaxLength(1000).IsRequired();

        // Una etapa de un vencimiento se avisa una sola vez, aunque el cron corra dos veces
        // a la vez.
        builder.HasIndex(a => new { a.SuscripcionId, a.PagaHasta, a.Estado }).IsUnique();
        builder.HasIndex(a => a.TenantId);

        builder.HasOne(a => a.Tenant)
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Suscripcion)
            .WithMany()
            .HasForeignKey(a => a.SuscripcionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PagoConfiguration : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> builder)
    {
        builder.ToTable("pagos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Monto).HasPrecision(12, 2);
        builder.Property(p => p.Medio).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Comprobante).HasMaxLength(120);
        builder.Property(p => p.Nota).HasMaxLength(500);

        builder.HasIndex(p => new { p.TenantId, p.Fecha });

        builder.HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Suscripcion)
            .WithMany(s => s.Pagos)
            .HasForeignKey(p => p.SuscripcionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
