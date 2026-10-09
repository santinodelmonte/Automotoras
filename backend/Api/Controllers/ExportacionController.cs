using System.IO.Compression;
using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Core.Analitica;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Exportacion;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Exportación completa de los datos de la automotora.
/// </summary>
/// <remarks>
/// La propuesta lo dice textualmente: los datos son de la automotora, y ante una baja se
/// entrega una exportación completa del stock y del histórico de demanda. Está disponible
/// para el dueño en cualquier momento, no solo ante una baja, y también con el sitio
/// suspendido, que es justamente cuando más se necesita.
/// <para>
/// No hay un solo filtro por tenant escrito a mano: el filtro global recorta todo al tenant
/// del token. Los eventos van agregados por día y no crudos, porque el crudo tiene la IP
/// hasheada y el <c>session_id</c> de los visitantes, que no le sirven a nadie y son datos
/// de terceros.
/// </para>
/// <para>
/// El ZIP se arma en memoria y se descarga directo: nada se escribe en el disco del
/// servidor, que en shared hosting IIS no es un lugar donde guardar nada.
/// </para>
/// </remarks>
[ApiController]
[Route("api/tenant/exportacion")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ExportacionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;

    public ExportacionController(AppDbContext db, TimeProvider reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    [HttpGet]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/zip")]
    public async Task<IActionResult> Descargar(CancellationToken cancellationToken)
    {
        var tenantId = User.TenantIdDelToken()
                       ?? throw new InvalidOperationException("La exportación requiere un tenant en el token.");

        // La tabla de tenants no tiene filtro global —es el catálogo de automotoras—, así
        // que acá el tenant va explícito.
        var slug = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var archivos = new Dictionary<string, Csv>
        {
            ["vehiculos.csv"] = await VehiculosAsync(cancellationToken).ConfigureAwait(false),
            ["fotos.csv"] = await FotosAsync(cancellationToken).ConfigureAwait(false),
            ["consultas_por_dia.csv"] = await EventosPorDiaAsync(soloConsultas: true, cancellationToken).ConfigureAwait(false),
            ["eventos_por_dia.csv"] = await EventosPorDiaAsync(soloConsultas: false, cancellationToken).ConfigureAwait(false),
            ["busquedas_sin_resultado.csv"] = await BusquedasSinResultadoAsync(cancellationToken).ConfigureAwait(false),
        };

        using var memoria = new MemoryStream();

        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            await Agregar(zip, "LEEME.txt", System.Text.Encoding.UTF8.GetBytes(Leeme)).ConfigureAwait(false);

            foreach (var (nombre, csv) in archivos)
            {
                await Agregar(zip, nombre, csv.ABytes()).ConfigureAwait(false);
            }
        }

        var fecha = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        return File(memoria.ToArray(), "application/zip", $"exportacion-{slug ?? "automotora"}-{fecha}.zip");
    }

    private async Task<Csv> VehiculosAsync(CancellationToken cancellationToken)
    {
        var vehiculos = await _db.Vehiculos
            .AsNoTracking()
            .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
            .Include(v => v.Version)
            .OrderBy(v => v.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var csv = new Csv(
            "id", "marca", "modelo", "version", "anio", "kilometraje", "combustible", "transmision",
            "color", "puertas", "motor", "precio", "moneda", "estado", "destacado", "precio_costo",
            "fecha_publicacion", "fecha_venta", "precio_venta", "descripcion", "creado", "modificado");

        foreach (var v in vehiculos)
        {
            csv.Fila(
                v.Id, v.Modelo?.Marca?.Nombre, v.Modelo?.Nombre, v.Version?.Nombre, v.Anio, v.Kilometraje,
                v.Combustible.ToString(), v.Transmision.ToString(), v.Color, v.Puertas, v.Motor, v.Precio,
                v.Moneda.ToString(), v.Estado.ToString(), v.Destacado, v.PrecioCosto, v.FechaPublicacion,
                v.FechaVenta, v.PrecioVenta, v.Descripcion, v.CreatedAt, v.UpdatedAt);
        }

        return csv;
    }

    private async Task<Csv> FotosAsync(CancellationToken cancellationToken)
    {
        var fotos = await _db.VehiculoFotos
            .AsNoTracking()
            .OrderBy(f => f.VehiculoId).ThenBy(f => f.Orden)
            .Select(f => new { f.VehiculoId, f.Orden, f.EsPortada, f.Url })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var csv = new Csv("vehiculo_id", "orden", "es_portada", "url");

        foreach (var f in fotos)
        {
            csv.Fila(f.VehiculoId, f.Orden, f.EsPortada, f.Url);
        }

        return csv;
    }

    /// <summary>Eventos contados por día, vehículo y tipo. Sin IP ni sesión.</summary>
    private async Task<Csv> EventosPorDiaAsync(bool soloConsultas, CancellationToken cancellationToken)
    {
        var eventos = _db.Eventos.AsNoTracking();

        if (soloConsultas)
        {
            eventos = eventos.Where(e => EventosDeContacto.Tipos.Contains(e.Tipo));
        }

        // Año, mes y día por separado y no .Date: se traduce igual en MySQL y en SQLite, y
        // la agregación la hace la base en vez de traer la tabla que más crece al proceso.
        var filas = await eventos
            .GroupBy(e => new { e.CreatedAt.Year, e.CreatedAt.Month, e.CreatedAt.Day, e.VehiculoId, e.Tipo })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.VehiculoId, g.Key.Tipo, Cantidad = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var csv = new Csv("fecha", "vehiculo_id", "tipo", "cantidad");

        foreach (var f in filas
                     .OrderBy(f => f.Year).ThenBy(f => f.Month).ThenBy(f => f.Day)
                     .ThenBy(f => f.VehiculoId).ThenBy(f => f.Tipo))
        {
            csv.Fila(new DateOnly(f.Year, f.Month, f.Day), f.VehiculoId, f.Tipo.ToString(), f.Cantidad);
        }

        return csv;
    }

    private async Task<Csv> BusquedasSinResultadoAsync(CancellationToken cancellationToken)
    {
        var busquedas = await _db.Busquedas
            .AsNoTracking()
            .Where(b => b.ResultadosCount == 0)
            .OrderBy(b => b.CreatedAt)
            .Select(b => new { b.CreatedAt, b.Filtros })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var csv = new Csv("fecha", "filtros");

        foreach (var b in busquedas)
        {
            csv.Fila(b.CreatedAt, b.Filtros);
        }

        return csv;
    }

    private static async Task Agregar(ZipArchive zip, string nombre, byte[] contenido)
    {
        var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal);
        var flujo = entrada.Open();

        await using (flujo.ConfigureAwait(false))
        {
            await flujo.WriteAsync(contenido).ConfigureAwait(false);
        }
    }

    private const string Leeme =
        "Exportación completa de los datos de la automotora.\r\n\r\n" +
        "vehiculos.csv                 Todo el stock, incluido lo vendido y lo pausado, con todos sus campos.\r\n" +
        "fotos.csv                     Las fotos de cada vehículo, en orden, con su dirección web.\r\n" +
        "consultas_por_dia.csv         Toques en WhatsApp y teléfono, por día y por vehículo.\r\n" +
        "eventos_por_dia.csv           Todo lo medido en el sitio (vistas, listados, consultas), por día y por vehículo.\r\n" +
        "busquedas_sin_resultado.csv   Lo que buscaron los visitantes y no encontraron, con los filtros usados.\r\n\r\n" +
        "Fechas en UTC y en formato año-mes-día. Números con punto decimal.\r\n" +
        "Los datos de los visitantes (IP, sesión) no se incluyen: son de terceros.\r\n";
}
