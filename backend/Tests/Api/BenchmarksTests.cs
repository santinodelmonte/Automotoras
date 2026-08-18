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
    }

    [Fact]
    public async Task Con_suficientes_automotoras_se_publica_el_promedio_de_mercado()
    {
        // Cuatro automotoras ajenas con cuatro ventas cada una: pasa los dos umbrales.
        SembrarVentasDeOtrasAutomotoras(automotoras: 4, ventasPorAutomotora: 4, diasParaVender: 40);
        SembrarVentasPropias(ventas: 2, diasParaVender: 60);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark?dias=365");

        Assert.NotNull(benchmark);

        var hatchback = benchmark.DiasParaVenderPorCarroceria
            .Single(c => c.Dimension == nameof(Carroceria.Hatchback));

        Assert.True(hatchback.AutomotorasAportantes >= UmbralesDeBenchmark.AutomotorasMinimas);
        Assert.True(hatchback.RegistrosAportantes >= UmbralesDeBenchmark.RegistrosMinimos);
        Assert.Equal(40, hatchback.Mercado);
        Assert.Equal(60, hatchback.Propio);
        Assert.Contains("días más que el resto", hatchback.Lectura, StringComparison.Ordinal);
    }

    /// <summary>
    /// La garantía que sostiene todo: de acá salen promedios, nunca datos de otra
    /// automotora. Se revisa el JSON crudo, no el objeto ya tipado.
    /// </summary>
    [Fact]
    public async Task La_respuesta_no_contiene_datos_identificables_de_otra_automotora()
    {
        SembrarVentasDeOtrasAutomotoras(automotoras: 4, ventasPorAutomotora: 4, diasParaVender: 30);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var json = await cliente.GetStringAsync("/api/reportes/benchmark?dias=365");

        foreach (var rastro in new[] { "tenantId", "slug", "automotora-vecina", "Automotora Sur", "sur" })
        {
            Assert.DoesNotContain(rastro, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Una carrocería con pocas automotoras detrás se omite entera. No se devuelve
    /// recortada ni con ceros: publicar "sin datos suficientes" por carrocería ya diría algo
    /// sobre quién vendió qué.
    /// </summary>
    [Fact]
    public async Task Una_carroceria_sin_suficientes_automotoras_se_omite_entera()
    {
        SembrarVentasDeOtrasAutomotoras(automotoras: 4, ventasPorAutomotora: 4, diasParaVender: 35);

        // Una sola automotora ajena vendiendo pickups: por debajo del umbral.
        SembrarVentasDeOtrasAutomotoras(
            automotoras: 1, ventasPorAutomotora: 12, diasParaVender: 20, carroceria: Carroceria.Pickup);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark?dias=365");

        Assert.NotNull(benchmark);
        Assert.Contains(benchmark.DiasParaVenderPorCarroceria, c => c.Dimension == nameof(Carroceria.Hatchback));
        Assert.DoesNotContain(benchmark.DiasParaVenderPorCarroceria, c => c.Dimension == nameof(Carroceria.Pickup));
    }

    private void SembrarVentasDeOtrasAutomotoras(
        int automotoras,
        int ventasPorAutomotora,
        int diasParaVender,
        Carroceria carroceria = Carroceria.Hatchback)
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

    private void SembrarVentasPropias(int ventas, int diasParaVender)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var _ = db.PermitirEscrituraCrossTenant();

        for (var i = 0; i < ventas; i++)
        {
            db.Vehiculos.Add(Vendido(_api.TenantNorte, _api.ModeloId, diasParaVender));
        }

        db.SaveChanges();
    }

    /// <summary>Devuelve un modelo de esa carrocería, creándolo si hace falta.</summary>
    private static int ModeloDe(AppDbContext db, Carroceria carroceria)
    {
        var existente = db.Modelos.FirstOrDefault(m => m.Carroceria == carroceria);

        if (existente is not null)
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
