using System.Globalization;
using System.Text.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// Stock de desarrollo con su historia de demanda.
/// </summary>
/// <remarks>
/// Los vehículos sin eventos no sirven para nada: el dashboard queda en cero y no hay
/// forma de ver si los reportes están bien hasta que el producto lleve meses en
/// producción. Por eso el seed genera además noventa días de comportamiento plausible.
/// <para>
/// Todo sale de un <c>Random</c> con semilla fija. Un seed que cambia en cada corrida
/// hace que un número raro en una pantalla no se pueda reproducir, y depurar contra datos
/// que ya no existen es imposible.
/// </para>
/// </remarks>
public static class SeedDeVehiculos
{
    private const int Semilla = 20260818;
    private const int DiasDeHistoria = 90;

    public static async Task EjecutarAsync(
        AppDbContext db,
        TimeProvider reloj,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(reloj);

        if (await db.Vehiculos.IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var azar = new Random(Semilla);

        var tenants = await db.Tenants
            .OrderBy(t => t.Id)
            .Where(t => t.Activo)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var modelos = await db.Modelos
            .OrderBy(m => m.Id)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenants.Count == 0 || modelos.Count == 0)
        {
            return;
        }

        var vehiculos = new List<Vehiculo>();

        foreach (var tenantId in tenants)
        {
            var cuantos = azar.Next(9, 14);

            for (var i = 0; i < cuantos; i++)
            {
                vehiculos.Add(NuevoVehiculo(tenantId, modelos[azar.Next(modelos.Count)], ahora, azar));
            }
        }

        db.Vehiculos.AddRange(vehiculos);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AgregarFotos(db, vehiculos);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AgregarEventos(db, vehiculos, ahora, azar);
        AgregarBusquedas(db, tenants, ahora, azar);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Vehiculo NuevoVehiculo(int tenantId, int modeloId, DateTime ahora, Random azar)
    {
        var publicacion = ahora.AddDays(-azar.Next(1, DiasDeHistoria)).Date;
        var estado = SortearEstado(azar);
        var enDolares = azar.Next(10) < 8; // el mercado uruguayo publica casi todo en USD

        var precioUsd = azar.Next(6, 46) * 1000m + azar.Next(0, 10) * 100m;

        var vehiculo = new Vehiculo
        {
            TenantId = tenantId,
            ModeloId = modeloId,
            Anio = ahora.Year - azar.Next(0, 12),
            Kilometraje = azar.Next(0, 210) * 1000,
            Combustible = SortearCombustible(azar),
            Transmision = azar.Next(2) == 0 ? Transmision.Manual : Transmision.Automatica,
            Color = Colores[azar.Next(Colores.Length)],
            Puertas = azar.Next(2) == 0 ? 4 : 5,
            Motor = $"{(azar.Next(10, 30) / 10m).ToString("0.0", CultureInfo.InvariantCulture)}",
            Precio = enDolares ? precioUsd : Math.Round(precioUsd * 40m, 0),
            Moneda = enDolares ? Moneda.Usd : Moneda.Uyu,
            Estado = estado,
            Descripcion = "Único dueño, service al día, papeles al día. Se acepta permuta.",
            Destacado = azar.Next(6) == 0 && estado == EstadoVehiculo.Disponible,

            // El costo es el precio menos un margen plausible. Es lo que hace que el
            // reporte de margen de fase 2 tenga con qué trabajar el día que exista.
            PrecioCosto = Math.Round(precioUsd * (enDolares ? 1m : 40m) * (0.80m + azar.Next(0, 10) / 100m), 0),
            FechaPublicacion = publicacion,
        };

        if (estado == EstadoVehiculo.Vendido)
        {
            var dias = Math.Max(1, (int)(ahora - publicacion).TotalDays);

            vehiculo.FechaVenta = publicacion.AddDays(azar.Next(1, dias + 1));
            vehiculo.PrecioVenta = Math.Round(vehiculo.Precio * (0.90m + azar.Next(0, 10) / 100m), 0);
        }

        return vehiculo;
    }

    private static void AgregarFotos(AppDbContext db, IReadOnlyList<Vehiculo> vehiculos)
    {
        foreach (var vehiculo in vehiculos)
        {
            // Placeholders remotos, determinísticos por id. El seed no sube binarios a
            // ningún storage: es dato de desarrollo, no contenido real.
            for (var orden = 0; orden < 3; orden++)
            {
                db.VehiculoFotos.Add(new VehiculoFoto
                {
                    VehiculoId = vehiculo.Id,
                    Url = $"https://picsum.photos/seed/auto{vehiculo.Id}-{orden}/1200/800",
                    UrlThumb = $"https://picsum.photos/seed/auto{vehiculo.Id}-{orden}/400/300",
                    Orden = orden,
                    EsPortada = orden == 0,
                });
            }
        }
    }

    /// <summary>
    /// Genera vistas y consultas con una forma parecida a la real: la mayoría de las
    /// unidades junta poco tráfico y unas pocas se llevan casi todo, y las consultas son
    /// una fracción chica de las vistas.
    /// </summary>
    private static void AgregarEventos(AppDbContext db, IReadOnlyList<Vehiculo> vehiculos, DateTime ahora, Random azar)
    {
        foreach (var vehiculo in vehiculos)
        {
            var diasPublicado = Math.Max(1, (int)(ahora - vehiculo.FechaPublicacion).TotalDays);

            // Una de cada seis unidades es la que se lleva la atención.
            var popular = azar.Next(6) == 0;
            var vistas = popular ? azar.Next(80, 200) : azar.Next(5, 45);

            for (var i = 0; i < vistas; i++)
            {
                var cuando = vehiculo.FechaPublicacion
                    .AddDays(azar.Next(0, diasPublicado))
                    .AddHours(azar.Next(8, 23))
                    .AddMinutes(azar.Next(0, 60));

                if (cuando > ahora)
                {
                    continue;
                }

                var sesion = Sesion(azar);

                db.Eventos.Add(NuevoEvento(vehiculo, TipoEvento.ViewFicha, cuando, sesion));

                // Alrededor de un ocho por ciento de las vistas termina en contacto. Es la
                // proporción que después mira el reporte de vistas contra consultas.
                if (azar.Next(100) < 8)
                {
                    var tipo = azar.Next(3) == 0 ? TipoEvento.ClickTelefono : TipoEvento.ClickWhatsapp;

                    db.Eventos.Add(NuevoEvento(vehiculo, tipo, cuando.AddMinutes(azar.Next(1, 10)), sesion));
                }
            }
        }
    }

    /// <summary>
    /// Búsquedas del sitio público, incluidas las que no encontraron nada. Esas últimas
    /// son la señal más valiosa del producto: dicen qué le están pidiendo a la automotora
    /// que no tiene en stock.
    /// </summary>
    private static void AgregarBusquedas(AppDbContext db, IReadOnlyList<int> tenants, DateTime ahora, Random azar)
    {
        foreach (var tenantId in tenants)
        {
            var cuantas = azar.Next(40, 90);

            for (var i = 0; i < cuantas; i++)
            {
                var cuando = ahora.AddDays(-azar.Next(0, DiasDeHistoria)).AddHours(azar.Next(8, 23));
                var sinResultado = azar.Next(4) == 0;
                var filtros = FiltrosSinteticos(azar);
                var sesion = Sesion(azar);

                db.Busquedas.Add(new Busqueda
                {
                    TenantId = tenantId,
                    Filtros = filtros,
                    ResultadosCount = sinResultado ? 0 : azar.Next(1, 12),
                    SessionId = sesion,
                    CreatedAt = cuando,
                });

                if (sinResultado)
                {
                    db.Eventos.Add(new Evento
                    {
                        TenantId = tenantId,
                        Tipo = TipoEvento.BusquedaSinResultado,
                        SessionId = sesion,
                        Metadata = filtros,
                        CreatedAt = cuando,
                    });
                }
            }
        }
    }

    private static Evento NuevoEvento(Vehiculo vehiculo, TipoEvento tipo, DateTime cuando, string sesion) => new()
    {
        TenantId = vehiculo.TenantId,
        VehiculoId = vehiculo.Id,
        Tipo = tipo,
        SessionId = sesion,
        CreatedAt = cuando,
        UserAgent = "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/126 Mobile Safari/537.36",
    };

    private static string FiltrosSinteticos(Random azar)
    {
        var carroceria = Enum.GetValues<Carroceria>()[azar.Next(Enum.GetValues<Carroceria>().Length)];

        return JsonSerializer.Serialize(new
        {
            Carroceria = carroceria.ToString(),
            AnioDesde = 2015 + azar.Next(0, 8),
            Moneda = "Usd",
            PrecioHasta = azar.Next(10, 40) * 1000,
        });
    }

    private static string Sesion(Random azar) => $"seed-{azar.Next(1, 900):000}";

    private static EstadoVehiculo SortearEstado(Random azar) => azar.Next(100) switch
    {
        < 68 => EstadoVehiculo.Disponible,
        < 78 => EstadoVehiculo.Reservado,
        < 94 => EstadoVehiculo.Vendido,
        _ => EstadoVehiculo.Pausado,
    };

    private static Combustible SortearCombustible(Random azar) => azar.Next(100) switch
    {
        < 55 => Combustible.Nafta,
        < 85 => Combustible.Diesel,
        < 92 => Combustible.Hibrido,
        < 97 => Combustible.Electrico,
        _ => Combustible.Gnc,
    };

    private static readonly string[] Colores =
        ["Blanco", "Gris", "Negro", "Plata", "Rojo", "Azul", "Beige", "Verde"];
}
