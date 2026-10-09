using AutomotoraSaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutomotoraSaaS.Infrastructure.Persistence.Configurations;

public sealed class EventoConfiguration : IEntityTypeConfiguration<Evento>
{
    public void Configure(EntityTypeBuilder<Evento> builder)
    {
        builder.ToTable("eventos");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SessionId).HasMaxLength(64);
        builder.Property(e => e.Metadata).HasColumnType("json");

        // Índice obligatorio del brief. Es el que sostiene todos los reportes: esta tabla
        // crece rápido y sin él cualquier agregación termina en full scan.
        builder.HasIndex(e => new { e.TenantId, e.VehiculoId, e.Tipo, e.CreatedAt });

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Los eventos sobreviven al vehículo: borrar una unidad no puede borrar la
        // historia de demanda que generó.
        builder.HasOne(e => e.Vehiculo)
            .WithMany()
            .HasForeignKey(e => e.VehiculoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class BusquedaConfiguration : IEntityTypeConfiguration<Busqueda>
{
    public void Configure(EntityTypeBuilder<Busqueda> builder)
    {
        builder.ToTable("busquedas");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Filtros).HasColumnType("json").IsRequired();
        builder.Property(b => b.SessionId).HasMaxLength(64);

        // Las búsquedas sin resultados se consultan por tenant y fecha: son la señal de
        // demanda insatisfecha que alimenta las sugerencias de compra de fase 2.
        builder.HasIndex(b => new { b.TenantId, b.ResultadosCount, b.CreatedAt });

        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CotizacionConfiguration : IEntityTypeConfiguration<Cotizacion>
{
    public void Configure(EntityTypeBuilder<Cotizacion> builder)
    {
        builder.ToTable("cotizaciones");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UsdUyu).HasPrecision(10, 4);

        // Una fila por día: el job que la puebla puede correr más de una vez sin duplicar.
        builder.HasIndex(c => c.Fecha).IsUnique();
    }
}

public sealed class PrecioDeMercadoConfiguration : IEntityTypeConfiguration<PrecioDeMercado>
{
    public void Configure(EntityTypeBuilder<PrecioDeMercado> builder)
    {
        builder.ToTable("precios_de_mercado");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Fuente).HasMaxLength(40).IsRequired();
        builder.Property(p => p.PrecioMediano).HasPrecision(12, 2);
        builder.Property(p => p.PrecioMinimo).HasPrecision(12, 2);
        builder.Property(p => p.PrecioMaximo).HasPrecision(12, 2);

        // Un snapshot por fuente, modelo, año y día: el job puede reintentar sin duplicar,
        // que es lo que lo hace disparable por un cron con reintentos.
        builder.HasIndex(p => new { p.Fuente, p.ModeloId, p.Anio, p.Fecha }).IsUnique();

        // Y este es el de lectura: siempre se pide el último precio de un modelo y año.
        builder.HasIndex(p => new { p.ModeloId, p.Anio, p.Fecha });

        // Restrict y no Cascade: un modelo que se borra del catálogo no puede llevarse la
        // historia de precios, que es de todas las automotoras y no se puede reconstruir.
        builder.HasOne(p => p.Modelo)
            .WithMany()
            .HasForeignKey(p => p.ModeloId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
