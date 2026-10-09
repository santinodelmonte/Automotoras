using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// Datos mínimos para poder trabajar en desarrollo: siete automotoras con sus usuarios y
/// el catálogo de marcas y modelos del mercado uruguayo.
/// </summary>
/// <remarks>
/// Idempotente: se puede correr en cada arranque. Solo se ejecuta en Development y solo
/// si hay una contraseña configurada en <c>Seed:Password</c>; nunca inventa una por
/// omisión, porque una contraseña por defecto que sobreviva a producción es exactamente
/// la clase de cosa que nadie nota hasta que es tarde.
/// <para>
/// El stock y los noventa días de eventos sintéticos los agrega
/// <see cref="SeedDeVehiculos"/>. Sin eventos, el dashboard queda en cero y no hay forma
/// de ver si los reportes están bien hasta que el producto lleve meses en producción.
/// </para>
/// </remarks>
public static class SeedDeDesarrollo
{
    public static async Task EjecutarAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        TimeProvider reloj,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(reloj);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // El seed escribe datos de varios tenants —y un SuperAdmin sin tenant— sin ningún
        // tenant resuelto. Es justo lo que la política de escritura bloquea, así que se
        // declara explícito.
        using var _ = db.PermitirEscrituraCrossTenant();

        var hash = hasher.Hash(password);

        // IgnoreQueryFilters en todas las consultas del seed: sin tenant resuelto los
        // filtros globales devuelven cero filas, y un chequeo de idempotencia que siempre
        // ve la base vacía vuelve a insertar y choca contra los índices únicos.
        await SembrarTenantsAsync(db, hash, reloj, cancellationToken).ConfigureAwait(false);

        // Planes y estados de pago distintos para algunas: sin esto la cobranza, los topes y
        // la página de mantenimiento no se pueden ver en desarrollo.
        await SeedDeCobranza.EjecutarAsync(db, hash, reloj, cancellationToken).ConfigureAwait(false);

        await SembrarCatalogoAsync(db, cancellationToken).ConfigureAwait(false);

        // El stock y su historia de demanda van al final: necesitan las automotoras y el
        // catálogo ya cargados.
        await SeedDeVehiculos.EjecutarAsync(db, reloj, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SembrarTenantsAsync(
        AppDbContext db,
        string hash,
        TimeProvider reloj,
        CancellationToken cancellationToken)
    {
        foreach (var (slug, nombre, dominio, primario, whatsapp, direccion) in Automotoras)
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
                    Direccion = direccion,
                };

                db.Tenants.Add(tenant);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await AsegurarSuscripcionAsync(db, tenant.Id, reloj, cancellationToken).ConfigureAwait(false);
            await AsegurarUsuarioAsync(db, $"owner@{slug}.uy", $"Owner {nombre}", RolUsuario.Owner, tenant.Id, hash, cancellationToken).ConfigureAwait(false);
            await AsegurarUsuarioAsync(db, $"vendedor@{slug}.uy", $"Vendedor {nombre}", RolUsuario.Seller, tenant.Id, hash, cancellationToken).ConfigureAwait(false);
        }

        await AsegurarUsuarioAsync(db, "super@automotoras.uy", "Super Admin", RolUsuario.SuperAdmin, null, hash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Plan Full para todas, de entrada. <see cref="SeedDeCobranza"/> después le cambia el
    /// plan y el estado de pago a algunas, para poder ver los demás casos.
    /// </summary>
    private static async Task AsegurarSuscripcionAsync(
        AppDbContext db,
        int tenantId,
        TimeProvider reloj,
        CancellationToken cancellationToken)
    {
        var tiene = await db.Suscripciones
            .IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == tenantId && s.Fin == null, cancellationToken)
            .ConfigureAwait(false);

        if (tiene)
        {
            return;
        }

        var full = await db.Planes
            .SingleAsync(p => p.Codigo == CodigosDePlan.Full, cancellationToken)
            .ConfigureAwait(false);

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        db.Suscripciones.Add(Suscripciones.IniciarConBonificacion(tenantId, full, hoy));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Las automotoras de desarrollo.
    /// </summary>
    /// <remarks>
    /// Son siete y no dos por el benchmark: la comparación contra el mercado no publica
    /// nada si no hay al menos cinco automotoras además de la que pregunta
    /// (<see cref="Core.Reportes.ReglasDelBenchmark"/>). Con dos o tres, la pantalla dice
    /// —correctamente— que no hay muestra suficiente, y entonces la única forma de saber
    /// si el reporte funciona es esperar a tener clientes reales.
    /// <para>
    /// Cada una tiene su ciudad y su color: un seed donde todas comparten la dirección
    /// hace que el branding por tenant, que es la mitad del producto, se vea igual en las
    /// siete.
    /// </para>
    /// </remarks>
    private static readonly (string Slug, string Nombre, string Dominio, string ColorPrimario, string Whatsapp, string Direccion)[] Automotoras =
    [
        ("norte", "Automotora Norte", "automotoranorte.uy", "#059669", "+59899111222", "Av. Italia 3821, Montevideo"),
        ("sur", "Automotora Sur", "automotorasur.uy", "#2563eb", "+59899333444", "Av. Luis A. de Herrera 1248, Montevideo"),
        ("costa", "Autos de la Costa", "autosdelacosta.uy", "#ea580c", "+59899555666", "Av. Roosevelt 1520, Punta del Este"),
        ("centenario", "Automotora Centenario", "centenarioautos.uy", "#7c3aed", "+59899777888", "Bvar. Artigas 2455, Montevideo"),
        ("litoral", "Litoral Automotores", "litoralautomotores.uy", "#dc2626", "+59899101112", "Av. Uruguay 890, Paysandú"),
        ("prado", "Prado Motors", "pradomotors.uy", "#0891b2", "+59899131415", "Av. Agraciada 3710, Montevideo"),
        ("estenia", "Estenia Autos", "esteniaautos.uy", "#ca8a04", "+59899161718", "Ruta 8 km 24, Canelones"),
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
