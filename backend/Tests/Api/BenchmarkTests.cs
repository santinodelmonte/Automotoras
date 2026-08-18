using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Reportes;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El benchmark anonimizado: el único lugar donde un endpoint de tenant lee datos de
/// otros.
/// </summary>
/// <remarks>
/// Lo que se prueba acá no es que el número dé bien, sino que no se publique cuando la
/// muestra es chica. Un promedio de dos competidores no es un agregado: cada uno despeja
/// al otro con una resta, y en un mercado chico sabe perfectamente quiénes son.
/// </remarks>
public sealed class BenchmarkTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public BenchmarkTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_vendedor_no_ve_el_benchmark()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/reportes/benchmark");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// Con muestra suficiente sí se publica, y lo que sale del lado del mercado es la
    /// mediana entre automotoras: la grande y la chica pesan lo mismo.
    /// </summary>
    [Fact]
    public async Task Con_muestra_suficiente_el_mercado_es_la_mediana_entre_automotoras()
    {
        // Cinco pares, cada uno con tres unidades publicadas hace 10, 20, 30, 40 y 50
        // días. La mediana entre automotoras es 30.
        SembrarPares([10, 20, 30, 40, 50]);

        // Y una tercera unidad para Norte, que con dos no llegaba al mínimo para aportar
        // —ni siquiera para verse a sí misma en el reporte—.
        _api.ConLaBase(db => db.Vehiculos.Add(Vehiculo(_api.TenantNorte, diasPublicado: 5)));

        var benchmark = await BenchmarkAsync();

        Assert.True(benchmark.Disponible);
        Assert.Null(benchmark.Motivo);
        Assert.Equal(5, benchmark.AutomotorasEnLaMuestra);

        Assert.NotNull(benchmark.DiasEnGondola);
        Assert.Equal(30m, benchmark.DiasEnGondola.Mercado);
        Assert.NotNull(benchmark.DiasEnGondola.Propio);
        Assert.True(benchmark.DiasEnGondola.MejorCuandoBaja);
    }

    /// <summary>
    /// Una automotora con una sola unidad no aporta: su mediana es su único vehículo, y
    /// publicarla sería publicar el dato de una automotora concreta con otro nombre.
    /// </summary>
    [Fact]
    public async Task Una_automotora_con_una_sola_unidad_no_entra_en_la_muestra()
    {
        SembrarPares([10, 20, 30, 40, 50]);

        _api.ConLaBase(db =>
        {
            var chica = new Tenant { Slug = "chica", Nombre = "Automotora Chica" };
            db.Tenants.Add(chica);
            db.SaveChanges();

            db.Vehiculos.Add(Vehiculo(chica.Id, diasPublicado: 300));
        });

        var benchmark = await BenchmarkAsync();

        // Sigue habiendo cinco: la que tiene una sola unidad no suma.
        Assert.Equal(5, benchmark.AutomotorasEnLaMuestra);
        Assert.Equal(30m, benchmark.DiasEnGondola?.Mercado);
    }

    /// <summary>
    /// Una métrica sin muestra suficiente no se publica aunque el resto del benchmark sí.
    /// Ninguno de los pares vendió nada, y los que no tienen el dato no cuentan como cero:
    /// no tardaron cero días en vender, no vendieron.
    /// </summary>
    [Fact]
    public async Task Una_metrica_sin_datos_de_los_pares_queda_vacia()
    {
        SembrarPares([10, 20, 30, 40, 50]);

        var benchmark = await BenchmarkAsync();

        Assert.True(benchmark.Disponible);
        Assert.NotNull(benchmark.DiasHastaLaVenta);
        Assert.Null(benchmark.DiasHastaLaVenta.Mercado);
    }

    /// <summary>Cinco automotoras, con tres unidades cada una publicadas hace los días que se pidan.</summary>
    private void SembrarPares(IReadOnlyList<int> dias)
    {
        _api.ConLaBase(db =>
        {
            for (var i = 0; i < dias.Count; i++)
            {
                var slug = $"par-{i}";

                if (db.Tenants.Any(t => t.Slug == slug))
                {
                    continue;
                }

                var tenant = new Tenant { Slug = slug, Nombre = $"Automotora Par {i}" };
                db.Tenants.Add(tenant);
                db.SaveChanges();

                for (var j = 0; j < ReglasDelBenchmark.VehiculosMinimosParaAportar; j++)
                {
                    db.Vehiculos.Add(Vehiculo(tenant.Id, dias[i]));
                }
            }
        });
    }

    private Vehiculo Vehiculo(int tenantId, int diasPublicado) => new()
    {
        TenantId = tenantId,
        ModeloId = _api.ModeloId,
        Anio = 2018,
        Kilometraje = 80_000,
        Combustible = Combustible.Nafta,
        Transmision = Transmision.Manual,
        Precio = 14_000m,
        Moneda = Moneda.Usd,
        Estado = EstadoVehiculo.Disponible,
        FechaPublicacion = DateTime.UtcNow.AddDays(-diasPublicado),
    };

    private async Task<BenchmarkDto> BenchmarkAsync()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark?dias=90");

        Assert.NotNull(benchmark);

        return benchmark;
    }
}

/// <summary>
/// El caso de la muestra insuficiente, en su propia clase.
/// </summary>
/// <remarks>
/// Separado y no como un test más de <see cref="BenchmarkTests"/>: los tests de una clase
/// comparten la base de su fábrica, y cualquiera que siembre las cinco automotoras que
/// hacen falta para publicar dejaría a este comprobando lo contrario de lo que dice su
/// nombre —o pasando por casualidad, según el orden en que corran—.
/// </remarks>
public sealed class BenchmarkSinMuestraTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public BenchmarkSinMuestraTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// Con las automotoras del seed no hay nada que publicar, y la respuesta lo dice en
    /// vez de devolver un número redondeado que igual delataría al vecino.
    /// </summary>
    [Fact]
    public async Task Con_pocas_automotoras_no_se_publica_nada()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var benchmark = await cliente.GetFromJsonAsync<BenchmarkDto>("/api/reportes/benchmark");

        Assert.NotNull(benchmark);
        Assert.False(benchmark.Disponible);
        Assert.NotNull(benchmark.Motivo);
        Assert.Equal(0, benchmark.AutomotorasEnLaMuestra);
        Assert.Null(benchmark.DiasEnGondola);
        Assert.Null(benchmark.ConsultasPorCienVistas);
        Assert.Null(benchmark.DiasHastaLaVenta);
    }
}
