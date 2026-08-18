using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Benchmarks cross-tenant. Es el único lugar donde una automotora se apoya en datos de
/// otras, así que lo que se prueba acá no es solo que el número salga: es que ninguna otra
/// automotora sea identificable.
/// </summary>
/// <remarks>
/// Los tests comparten la base del fixture y xUnit no garantiza el orden dentro de una
/// clase, así que cada uno siembra sobre su propia carrocería. Sin eso, el promedio que
/// mide un test depende de si otro ya corrió — y un test que pasa o falla según el orden
/// no prueba nada.
/// </remarks>
public sealed class BenchmarksTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public BenchmarksTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_vendedor_no_entra_al_benchmark()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/reportes/benchmark");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// La invariante que sostiene todo el anonimato, y la única que vale sin importar qué
    /// más haya en la base: nada se publica por debajo de los umbrales.
    /// </summary>
    [Fact]
    public async Task Nada_se_publica_por_debajo_de_los_umbrales()
    {
        SembrarVentasDeOtrasAutomotoras(Carroceria.Sedan, automotoras: 4, ventasPorAutomotora: 4, diasParaVender: 30);
        SembrarVentasDeOtrasAutomotoras(Carroceria.Minivan, automotoras: 1, ventasPorAutomotora: 20, diasParaVender: 20);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark?dias=365");

        Assert.NotNull(benchmark);
        Assert.NotEmpty(benchmark.DiasParaVenderPorCarroceria);

        Assert.All(benchmark.DiasParaVenderPorCarroceria, c =>
        {
            Assert.True(
                c.AutomotorasAportantes >= UmbralesDeBenchmark.AutomotorasMinimas,
                $"{c.Dimension} se publicó con {c.AutomotorasAportantes} automotoras detrás.");

            Assert.True(
                c.RegistrosAportantes >= UmbralesDeBenchmark.RegistrosMinimos,
                $"{c.Dimension} se publicó con {c.RegistrosAportantes} registros detrás.");
        });

        // Y la que no llega al umbral se omite entera: no recortada, no en cero. Publicar
        // "sin datos suficientes" por carrocería ya diría algo sobre quién vendió qué.
        Assert.DoesNotContain(
            benchmark.DiasParaVenderPorCarroceria,
            c => c.Dimension == nameof(Carroceria.Minivan));
    }

    [Fact]
    public async Task Con_suficientes_automotoras_se_publica_el_promedio_de_mercado()
    {
        SembrarVentasDeOtrasAutomotoras(Carroceria.Suv, automotoras: 4, ventasPorAutomotora: 4, diasParaVender: 40);
        SembrarVentasPropias(Carroceria.Suv, ventas: 2, diasParaVender: 60);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark?dias=365");

        Assert.NotNull(benchmark);

        var suv = benchmark.DiasParaVenderPorCarroceria.Single(c => c.Dimension == nameof(Carroceria.Suv));

        Assert.Equal(4, suv.AutomotorasAportantes);
        Assert.Equal(16, suv.RegistrosAportantes);
        Assert.Equal(40, suv.Mercado);
        Assert.Equal(60, suv.Propio);
        Assert.Contains("días más que el resto", suv.Lectura, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sin ventas propias de esa carrocería, el promedio del mercado se muestra igual y lo
    /// propio queda en null. Nulo es "todavía no vendiste ninguna", no un cero que arruine
    /// la comparación.
    /// </summary>
    [Fact]
    public async Task Sin_ventas_propias_se_muestra_el_mercado_y_lo_propio_queda_en_null()
    {
        SembrarVentasDeOtrasAutomotoras(Carroceria.Coupe, automotoras: 4, ventasPorAutomotora: 3, diasParaVender: 25);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark?dias=365");

        Assert.NotNull(benchmark);

        var coupe = benchmark.DiasParaVenderPorCarroceria.Single(c => c.Dimension == nameof(Carroceria.Coupe));

        Assert.Null(coupe.Propio);
        Assert.Equal(25, coupe.Mercado);
        Assert.Contains("Todavía no vendiste", coupe.Lectura, StringComparison.Ordinal);
    }

    /// <summary>
    /// La garantía que sostiene todo: de acá salen promedios, nunca datos de otra
    /// automotora. Se revisa el JSON crudo, no el objeto ya tipado.
    /// </summary>
    [Fact]
    public async Task La_respuesta_no_contiene_datos_identificables_de_otra_automotora()
    {
        SembrarVentasDeOtrasAutomotoras(Carroceria.Van, automotoras: 4, ventasPorAutomotora: 4, diasParaVender: 30);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var json = await cliente.GetStringAsync("/api/reportes/benchmark?dias=365");

        foreach (var rastro in new[] { "tenantId", "slug", "vecina", "Automotora Sur" })
        {
            Assert.DoesNotContain(rastro, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SembrarVentasDeOtrasAutomotoras(
        Carroceria carroceria,
        int automotoras,
        int ventasPorAutomotora,
        int diasParaVender)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var _ = db.PermitirEscrituraCrossTenant();

        var modeloId = ModeloDe(db, carroceria);
        var sufijo = Guid.NewGuid().ToString("N")[..6];

        for (var i = 0; i < automotoras; i++)
        {
            var tenant = new Tenant { Slug = $"vecina-{sufijo}-{i}", Nombre = $"Vecina {sufijo} {i}" };
            db.Tenants.Add(tenant);
            db.SaveChanges();

            for (var v = 0; v < ventasPorAutomotora; v++)
            {
                db.Vehiculos.Add(Vendido(tenant.Id, modeloId, diasParaVender));
            }
        }

        db.SaveChanges();
    }

    private void SembrarVentasPropias(Carroceria carroceria, int ventas, int diasParaVender)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var _ = db.PermitirEscrituraCrossTenant();

        var modeloId = ModeloDe(db, carroceria);

        for (var i = 0; i < ventas; i++)
        {
            db.Vehiculos.Add(Vendido(_api.TenantNorte, modeloId, diasParaVender));
        }

        db.SaveChanges();
    }

    /// <summary>Devuelve un modelo de esa carrocería, creándolo si hace falta.</summary>
    private static int ModeloDe(AppDbContext db, Carroceria carroceria)
    {
        if (db.Modelos.FirstOrDefault(m => m.Carroceria == carroceria) is { } existente)
        {
            return existente.Id;
        }

        var marca = db.Marcas.First();
        var modelo = new Modelo { MarcaId = marca.Id, Nombre = $"Modelo {carroceria}", Carroceria = carroceria };

        db.Modelos.Add(modelo);
        db.SaveChanges();

        return modelo.Id;
    }

    private static Vehiculo Vendido(int tenantId, int modeloId, int diasParaVender)
    {
        var publicacion = DateTime.UtcNow.Date.AddDays(-(diasParaVender + 5));

        return new Vehiculo
        {
            TenantId = tenantId,
            ModeloId = modeloId,
            Anio = 2018,
            Kilometraje = 80_000,
            Combustible = Combustible.Nafta,
            Transmision = Transmision.Manual,
            Precio = 12_000m,
            Moneda = Moneda.Usd,
            Estado = EstadoVehiculo.Vendido,
            FechaPublicacion = publicacion,
            FechaVenta = publicacion.AddDays(diasParaVender),
            PrecioVenta = 11_500m,
        };
    }
}

/// <summary>
/// El caso de una automotora sola en el sistema.
/// </summary>
/// <remarks>
/// Va en su propia clase, con su propio fixture, porque lo que prueba es la <em>ausencia</em>
/// de datos: cualquier siembra de otro test lo invalidaría, y el fixture es por clase.
/// </remarks>
public sealed class BenchmarksSinMercadoTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public BenchmarksSinMercadoTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// Con pocas automotoras detrás no se publica nada. Con dos, quien pregunta conoce la
    /// suya y despeja la otra restando.
    /// </summary>
    [Fact]
    public async Task Sin_suficientes_automotoras_no_se_publica_ninguna_comparacion()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark?dias=365");

        Assert.NotNull(benchmark);
        Assert.Empty(benchmark.DiasParaVenderPorCarroceria);
        Assert.Null(benchmark.ConsultasPorCienVistas);
        Assert.Contains("identificable", benchmark.NotaDePrivacidad, StringComparison.OrdinalIgnoreCase);
    }
}
