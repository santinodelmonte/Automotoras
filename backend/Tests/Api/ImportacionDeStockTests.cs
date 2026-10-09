using System.Net;
using System.Net.Http.Json;
using System.Text;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Vehiculos;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Carga masiva de stock: validar sin escribir, todo o nada, y el tope del plan.
/// </summary>
public sealed class ImportacionDeStockTests : IClassFixture<FabricaDeApi>
{
    private const string Encabezado =
        "marca,modelo,version,anio,kilometraje,combustible,transmision,color,puertas,motor,precio,moneda,descripcion,destacado,precio_costo";

    private readonly FabricaDeApi _api;

    public ImportacionDeStockTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Validar_no_escribe_nada_y_confirmar_crea_todos()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("importa-ok", CodigosDePlan.Full);
        var csv = Archivo(
            "Volkswagen,Gol,,2018,85.000,Nafta,Manual,Gris,5,1.6,\"9.500\",Usd,\"Único dueño, impecable\",si,8000",
            "volkswagen,GOL,,2020,40000,nafta,manual,,,,12500.50,usd,,,");

        using var cliente = await _api.ClienteDeAsync(owner);

        var validacion = await Importar(cliente, csv, confirmar: false);
        Assert.Equal(2, validacion.Filas);
        Assert.Equal(2, validacion.Validas);
        Assert.Empty(validacion.Errores);
        Assert.Equal(0, validacion.Importados);
        Assert.Equal(0, Vehiculos(tenantId));

        var importacion = await Importar(cliente, csv, confirmar: true);
        Assert.Equal(2, importacion.Importados);

        _api.ConLaBase(db =>
        {
            var cargados = db.Vehiculos.IgnoreQueryFilters().Where(v => v.TenantId == tenantId).AsEnumerable().OrderBy(v => v.Precio).ToList();

            Assert.Equal(2, cargados.Count);
            Assert.Equal(9_500m, cargados[0].Precio);
            Assert.Equal(85_000, cargados[0].Kilometraje);
            Assert.Equal("Único dueño, impecable", cargados[0].Descripcion);
            Assert.True(cargados[0].Destacado);
            Assert.Equal(8_000m, cargados[0].PrecioCosto);
            Assert.Equal(12_500.50m, cargados[1].Precio);
            Assert.All(cargados, v => Assert.Equal(EstadoVehiculo.Disponible, v.Estado));
        });
    }

    [Fact]
    public async Task Una_sola_fila_con_error_no_deja_cargar_ninguna_y_el_informe_dice_cual()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("importa-error", CodigosDePlan.Full);
        var csv = Archivo(
            "Volkswagen,Gol,,2018,85000,Nafta,Manual,,,,9500,Usd,,,",
            "Volkswagen,Fusca,,2018,85000,Nafta,Manual,,,,9500,Usd,,,",
            "Volkswagen,Gol,,1900,85000,Nafta,Manual,,,,9500,Usd,,,",
            "Volkswagen,Gol,,2018,85000,Kerosene,Manual,,,,mucho,Usd,,,");

        using var cliente = await _api.ClienteDeAsync(owner);
        var resultado = await Importar(cliente, csv, confirmar: true);

        Assert.Equal(0, resultado.Importados);
        Assert.Equal(1, resultado.Validas);
        Assert.Equal(0, Vehiculos(tenantId));

        Assert.Contains(resultado.Errores, e => e.Fila == 3 && e.Columna == "modelo");
        Assert.Contains(resultado.Errores, e => e.Fila == 4 && e.Columna == "anio");
        Assert.Contains(resultado.Errores, e => e.Fila == 5 && e.Columna == "precio");
    }

    [Fact]
    public async Task El_punto_y_coma_de_Excel_en_castellano_tambien_sirve()
    {
        var (_, owner) = _api.AutomotoraConPlan("importa-excel", CodigosDePlan.Full);
        var csv = Encoding.UTF8.GetBytes(
            Encabezado.Replace(',', ';') + "\r\n" +
            "Volkswagen;Gol;;2018;85000;Nafta;Manual;;;;9.500,00;Usd;;no;\r\n");

        using var cliente = await _api.ClienteDeAsync(owner);
        var resultado = await Importar(cliente, csv, confirmar: false);

        Assert.Empty(resultado.Errores);
        Assert.Equal(1, resultado.Validas);
    }

    [Fact]
    public async Task La_importacion_respeta_el_tope_del_plan()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("importa-tope", CodigosDePlan.Vidriera);
        _api.ConLaBase(db => db.Vehiculos.AddRange(Enumerable.Range(0, 39).Select(_ => new Core.Entities.Vehiculo
        {
            TenantId = tenantId,
            ModeloId = _api.ModeloId,
            Anio = 2019,
            Kilometraje = 1,
            Combustible = Combustible.Nafta,
            Transmision = Transmision.Manual,
            Precio = 1m,
            Moneda = Moneda.Usd,
            FechaPublicacion = DateTime.UtcNow,
        })));

        var csv = Archivo(
            "Volkswagen,Gol,,2018,85000,Nafta,Manual,,,,9500,Usd,,,",
            "Volkswagen,Gol,,2019,85000,Nafta,Manual,,,,9500,Usd,,,");

        using var cliente = await _api.ClienteDeAsync(owner);
        var resultado = await Importar(cliente, csv, confirmar: true);

        Assert.Equal(0, resultado.Importados);
        Assert.Contains(resultado.Errores, e => e.Mensaje.Contains("plan", StringComparison.Ordinal));
        Assert.Equal(39, Vehiculos(tenantId));
    }

    [Fact]
    public async Task El_superadmin_importa_en_una_automotora_y_un_vendedor_no_puede()
    {
        var (tenantId, _) = _api.AutomotoraConPlan("importa-admin", CodigosDePlan.Full);
        var csv = Archivo("Volkswagen,Gol,,2018,85000,Nafta,Manual,,,,9500,Usd,,,");

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        using var contenido = Formulario(csv);
        var respuesta = await admin.PostAsync($"/api/admin/tenants/{tenantId}/importacion?confirmar=true", contenido);

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoDeImportacionDto>();
        Assert.Equal(1, resultado!.Importados);
        Assert.Equal(1, Vehiculos(tenantId));

        using var vendedor = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);
        using var otro = Formulario(csv);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.PostAsync("/api/vehiculos/importacion", otro)).StatusCode);
    }

    [Fact]
    public async Task La_plantilla_trae_todas_las_columnas_y_se_puede_importar_tal_cual()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var plantilla = await cliente.GetByteArrayAsync("/api/vehiculos/importacion/plantilla");
        var texto = Encoding.UTF8.GetString(plantilla).TrimStart('﻿');

        Assert.StartsWith(Encabezado, texto, StringComparison.Ordinal);
    }

    private async Task<ResultadoDeImportacionDto> Importar(HttpClient cliente, byte[] csv, bool confirmar)
    {
        using var contenido = Formulario(csv);
        var respuesta = await cliente.PostAsync($"/api/vehiculos/importacion?confirmar={confirmar}", contenido);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ResultadoDeImportacionDto>())!;
    }

    private static MultipartFormDataContent Formulario(byte[] csv)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(csv);
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        formulario.Add(archivo, "archivo", "stock.csv");
        return formulario;
    }

    private static byte[] Archivo(params string[] filas)
        => Encoding.UTF8.GetBytes(Encabezado + "\r\n" + string.Join("\r\n", filas) + "\r\n");

    private int Vehiculos(int tenantId)
    {
        var cantidad = 0;
        _api.ConLaBase(db => cantidad = db.Vehiculos.IgnoreQueryFilters().Count(v => v.TenantId == tenantId));
        return cantidad;
    }
}
