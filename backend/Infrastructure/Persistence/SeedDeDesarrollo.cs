using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// Datos mínimos para poder trabajar en desarrollo: dos automotoras con sus usuarios y el
/// catálogo de marcas y modelos del mercado uruguayo.
/// </summary>
/// <remarks>
/// Idempotente: se puede correr en cada arranque. Solo se ejecuta en Development y solo
/// si hay una contraseña configurada en <c>Seed:Password</c>; nunca inventa una por
/// omisión, porque una contraseña por defecto que sobreviva a producción es exactamente
/// la clase de cosa que nadie nota hasta que es tarde.
/// <para>
/// El stock de vehículos y los eventos sintéticos llegan con las features de fase 1: no
/// tiene sentido sembrar vehículos antes de que exista el ABM que los mantiene.
/// </para>
/// </remarks>
public static class SeedDeDesarrollo
{
    public static async Task EjecutarAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // El seed escribe datos de varios tenants —y un SuperAdmin sin tenant— sin ningún
        // tenant resuelto. Es justo lo que la política de escritura bloquea, así que se
        // declara explícito.
        using var _ = db.PermitirEscrituraCrossTenant();

        var hash = hasher.Hash(password);

        // IgnoreQueryFilters en todas las consultas del seed: sin tenant resuelto los
        // filtros globales devuelven cero filas, y un chequeo de idempotencia que siempre
        // ve la base vacía vuelve a insertar y choca contra los índices únicos.
        await SembrarTenantsAsync(db, hash, cancellationToken).ConfigureAwait(false);
        await SembrarCatalogoAsync(db, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SembrarTenantsAsync(AppDbContext db, string hash, CancellationToken cancellationToken)
    {
        foreach (var (slug, nombre, dominio, primario, whatsapp) in Automotoras)
        {
            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken)
                .ConfigureAwait(false);

            if (tenant is null)
            {
                tenant = new Tenant
                {
                    Slug = slug,
                    Nombre = nombre,
                    DominioCustom = dominio,
                    ColorPrimario = primario,
                    ColorSecundario = "#0f172a",
                    Whatsapp = whatsapp,
                    Telefono = whatsapp,
                    Direccion = "Av. Italia 1234, Montevideo",
                };

                db.Tenants.Add(tenant);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await AsegurarUsuarioAsync(db, $"owner@{slug}.uy", $"Owner {nombre}", RolUsuario.Owner, tenant.Id, hash, cancellationToken).ConfigureAwait(false);
            await AsegurarUsuarioAsync(db, $"vendedor@{slug}.uy", $"Vendedor {nombre}", RolUsuario.Seller, tenant.Id, hash, cancellationToken).ConfigureAwait(false);
        }

        await AsegurarUsuarioAsync(db, "super@automotoras.uy", "Super Admin", RolUsuario.SuperAdmin, null, hash, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AsegurarUsuarioAsync(
        AppDbContext db,
        string email,
        string nombre,
        RolUsuario rol,
        int? tenantId,
        string hash,
        CancellationToken cancellationToken)
    {
        var existe = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (existe)
        {
            return;
        }

        db.Users.Add(new User
        {
            TenantId = tenantId,
            Email = email,
            Nombre = nombre,
            Rol = rol,
            PasswordHash = hash,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SembrarCatalogoAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Marcas.IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (var (nombreMarca, modelos) in Catalogo)
        {
            var marca = new Marca { Nombre = nombreMarca };
            db.Marcas.Add(marca);

            foreach (var (nombreModelo, carroceria) in modelos)
            {
                marca.Modelos.Add(new Modelo { Nombre = nombreModelo, Carroceria = carroceria });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static readonly (string Slug, string Nombre, string Dominio, string ColorPrimario, string Whatsapp)[] Automotoras =
    [
        ("norte", "Automotora Norte", "automotoranorte.uy", "#059669", "+59899111222"),
        ("sur", "Automotora Sur", "automotorasur.uy", "#2563eb", "+59899333444"),
    ];

    /// <summary>
    /// Marcas y modelos reales del mercado uruguayo. La normalización es el cimiento de
    /// toda la analítica: si esto fuera texto libre, cualquier agregación posterior sería
    /// basura irrecuperable.
    /// </summary>
    private static readonly (string Marca, (string Modelo, Carroceria Carroceria)[] Modelos)[] Catalogo =
    [
        ("Chevrolet", [("Onix", Carroceria.Hatchback), ("Onix Plus", Carroceria.Sedan), ("Tracker", Carroceria.Suv), ("S10", Carroceria.Pickup), ("Spin", Carroceria.Minivan)]),
        ("Volkswagen", [("Gol", Carroceria.Hatchback), ("Polo", Carroceria.Hatchback), ("Virtus", Carroceria.Sedan), ("T-Cross", Carroceria.Suv), ("Amarok", Carroceria.Pickup), ("Saveiro", Carroceria.Pickup)]),
        ("Fiat", [("Argo", Carroceria.Hatchback), ("Cronos", Carroceria.Sedan), ("Mobi", Carroceria.Hatchback), ("Toro", Carroceria.Pickup), ("Strada", Carroceria.Pickup), ("Pulse", Carroceria.Suv)]),
        ("Toyota", [("Yaris", Carroceria.Hatchback), ("Corolla", Carroceria.Sedan), ("Corolla Cross", Carroceria.Suv), ("Hilux", Carroceria.Pickup), ("RAV4", Carroceria.Suv)]),
        ("Ford", [("Ka", Carroceria.Hatchback), ("EcoSport", Carroceria.Suv), ("Ranger", Carroceria.Pickup), ("Territory", Carroceria.Suv), ("Maverick", Carroceria.Pickup)]),
        ("Renault", [("Kwid", Carroceria.Hatchback), ("Sandero", Carroceria.Hatchback), ("Logan", Carroceria.Sedan), ("Duster", Carroceria.Suv), ("Oroch", Carroceria.Pickup), ("Kangoo", Carroceria.Van)]),
        ("Peugeot", [("208", Carroceria.Hatchback), ("2008", Carroceria.Suv), ("3008", Carroceria.Suv), ("Partner", Carroceria.Van), ("Landtrek", Carroceria.Pickup)]),
        ("Nissan", [("March", Carroceria.Hatchback), ("Versa", Carroceria.Sedan), ("Kicks", Carroceria.Suv), ("Frontier", Carroceria.Pickup)]),
        ("Hyundai", [("HB20", Carroceria.Hatchback), ("Creta", Carroceria.Suv), ("Tucson", Carroceria.Suv), ("Santa Fe", Carroceria.Suv)]),
        ("Kia", [("Picanto", Carroceria.Hatchback), ("Rio", Carroceria.Sedan), ("Sportage", Carroceria.Suv), ("Sorento", Carroceria.Suv)]),
        ("Suzuki", [("Swift", Carroceria.Hatchback), ("Baleno", Carroceria.Hatchback), ("Vitara", Carroceria.Suv), ("Jimny", Carroceria.Suv)]),
        ("Chery", [("Tiggo 2", Carroceria.Suv), ("Tiggo 4", Carroceria.Suv), ("Tiggo 7", Carroceria.Suv), ("Arrizo 5", Carroceria.Sedan)]),
        ("BYD", [("Dolphin", Carroceria.Hatchback), ("Song Plus", Carroceria.Suv), ("Yuan Plus", Carroceria.Suv)]),
        ("Citroën", [("C3", Carroceria.Hatchback), ("C4 Cactus", Carroceria.Suv), ("Berlingo", Carroceria.Van)]),
        ("Jeep", [("Renegade", Carroceria.Suv), ("Compass", Carroceria.Suv)]),
    ];
}
