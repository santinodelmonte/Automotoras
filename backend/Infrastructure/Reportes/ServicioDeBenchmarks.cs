using System.Globalization;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Reportes;

/// <summary>
/// Compara a una automotora contra el resto del mercado, sin que ninguna sea identificable.
/// </summary>
/// <remarks>
/// <b>Este es el único lugar del lado de tenant que lee datos de otras automotoras.</b>
/// Está en su propio archivo para que esa excepción se pueda auditar leyendo un archivo
/// corto, y no escondida entre trescientas líneas de otro servicio.
/// <para>
/// Las reglas que la hacen aceptable, todas verificadas por tests:
/// <list type="number">
///   <item>De acá solo salen promedios. Ninguna fila, ningún id, ningún nombre.</item>
///   <item>Un agregado se publica únicamente si hay al menos
///   <see cref="UmbralesDeBenchmark.AutomotorasMinimas"/> automotoras detrás, sin contar la
///   que pregunta. Con dos, quien pregunta conoce la suya y despeja la otra restando.</item>
///   <item>Y solo si hay al menos <see cref="UmbralesDeBenchmark.RegistrosMinimos"/>
///   registros: tres ventas no son un promedio de mercado.</item>
///   <item>Si un grupo no llega al umbral, no se devuelve recortado ni con ceros: no se
///   devuelve. Publicar "sin datos suficientes" por carrocería ya diría algo sobre quién
///   vendió qué.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class ServicioDeBenchmarks : IServicioDeBenchmarks
{
    private const string Nota =
        "Los promedios de mercado son agregados de varias automotoras. Solo se muestran las "
        + "comparaciones con suficientes automotoras detrás como para que ninguna sea "
        + "identificable; el resto se omite.";

    private static readonly TipoEvento[] EventosDeConsulta =
        [TipoEvento.ClickWhatsapp, TipoEvento.ClickTelefono];

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _reloj;

    public ServicioDeBenchmarks(AppDbContext db, ITenantContext tenantContext, TimeProvider reloj)
    {
        _db = db;
        _tenantContext = tenantContext;
        _reloj = reloj;
    }

    public async Task<BenchmarkDto> CompararAsync(int dias, CancellationToken cancellationToken = default)
    {
        var propio = _tenantContext.TenantId
                     ?? throw new InvalidOperationException(
                         "No hay tenant resuelto: el benchmark necesita saber quién pregunta para excluirlo del agregado.");

        var desde = _reloj.GetUtcNow().UtcDateTime.AddDays(-dias);

        var porCarroceria = await DiasParaVenderAsync(propio, desde, cancellationToken).ConfigureAwait(false);
        var consultas = await ConsultasPorCienVistasAsync(propio, desde, cancellationToken).ConfigureAwait(false);

        return new BenchmarkDto(dias, porCarroceria, consultas, Nota);
    }

    /// <summary>
    /// Cuántos días tarda en venderse cada carrocería, acá y en el resto del mercado.
    /// </summary>
    private async Task<IReadOnlyList<ComparativoDto>> DiasParaVenderAsync(
        int propio,
        DateTime desde,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters cross-tenant, la excepción de este archivo. Se proyecta a lo
        // mínimo indispensable —tenant, carrocería y las dos fechas— para que de la base no
        // salga nada más que lo que el agregado necesita.
        var vendidos = await _db.Vehiculos
            .IgnoreQueryFilters()
            .Where(v => v.Estado == EstadoVehiculo.Vendido
                        && v.FechaVenta != null
                        && v.FechaVenta >= desde)
            .Select(v => new
            {
                v.TenantId,
                Carroceria = v.Modelo!.Carroceria,
                v.FechaPublicacion,
                FechaVenta = v.FechaVenta!.Value,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var conDias = vendidos
            .Select(v => new
            {
                v.TenantId,
                v.Carroceria,
                Dias = MapeosDeVehiculo.DiasEnGondola(v.FechaPublicacion, v.FechaVenta, v.FechaVenta),
            })
            .ToList();

        var comparativos = new List<ComparativoDto>();

        foreach (var grupo in conDias.GroupBy(v => v.Carroceria).OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            var ajenos = grupo.Where(v => v.TenantId != propio).ToList();
            var automotoras = ajenos.Select(v => v.TenantId).Distinct().Count();

            if (automotoras < UmbralesDeBenchmark.AutomotorasMinimas
                || ajenos.Count < UmbralesDeBenchmark.RegistrosMinimos)
            {
                // No se publica ni recortado ni en cero: no se publica. Decir "sin datos
                // suficientes" por carrocería ya diría algo sobre quién vendió qué.
                continue;
            }

            var mios = grupo.Where(v => v.TenantId == propio).ToList();
            var mercado = Math.Round(ajenos.Average(v => v.Dias), 1);
            var mio = mios.Count > 0 ? Math.Round(mios.Average(v => v.Dias), 1) : (double?)null;

            comparativos.Add(new ComparativoDto(
                grupo.Key.ToString(),
                mio,
                mercado,
                automotoras,
                ajenos.Count,
                LecturaDeDias(mio, mercado)));
        }

        return comparativos;
    }

    /// <summary>
    /// Cuántas consultas genera cada cien visitas, acá y en el resto.
    /// </summary>
    /// <remarks>
    /// El promedio del mercado es el promedio de los ratios de cada automotora, no el ratio
    /// de los totales sumados. Si no, la automotora más grande decidiría sola el número y la
    /// comparación dejaría de significar algo para el resto.
    /// </remarks>
    private async Task<ComparativoDto?> ConsultasPorCienVistasAsync(
        int propio,
        DateTime desde,
        CancellationToken cancellationToken)
    {
        var conteos = await _db.Eventos
            .IgnoreQueryFilters()
            .Where(e => e.CreatedAt >= desde
                        && (e.Tipo == TipoEvento.ViewFicha || EventosDeConsulta.Contains(e.Tipo)))
            .GroupBy(e => new { e.TenantId, e.Tipo })
            .Select(g => new { g.Key.TenantId, g.Key.Tipo, Cantidad = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var porAutomotora = conteos
            .GroupBy(c => c.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Vistas = g.Where(c => c.Tipo == TipoEvento.ViewFicha).Sum(c => c.Cantidad),
                Consultas = g.Where(c => EventosDeConsulta.Contains(c.Tipo)).Sum(c => c.Cantidad),
            })
            // Una automotora con cuatro visitas metería ruido puro en el promedio.
            .Where(a => a.Vistas >= UmbralesDeBenchmark.VistasMinimasPorAutomotora)
            .Select(a => new { a.TenantId, Ratio = a.Consultas * 100d / a.Vistas })
            .ToList();

        var ajenas = porAutomotora.Where(a => a.TenantId != propio).ToList();

        if (ajenas.Count < UmbralesDeBenchmark.AutomotorasMinimas)
        {
            return null;
        }

        var mercado = Math.Round(ajenas.Average(a => a.Ratio), 1);
        var mio = porAutomotora.FirstOrDefault(a => a.TenantId == propio) is { } propia
            ? Math.Round(propia.Ratio, 1)
            : (double?)null;

        return new ComparativoDto(
            "Consultas cada 100 visitas",
            mio,
            mercado,
            ajenas.Count,
            ajenas.Count,
            LecturaDeConsultas(mio, mercado));
    }

    private static string LecturaDeDias(double? mio, double mercado)
    {
        if (mio is not { } propio)
        {
            return $"El resto del mercado tarda {mercado.ToString("0.#", CultureInfo.InvariantCulture)} días "
                   + "en vender esta carrocería. Todavía no vendiste ninguna para comparar.";
        }

        var diferencia = Math.Round(propio - mercado, 1);

        if (Math.Abs(diferencia) < 3)
        {
            return "Vendés esta carrocería en el mismo tiempo que el resto del mercado.";
        }

        return diferencia > 0
            ? $"Tardás {diferencia.ToString("0.#", CultureInfo.InvariantCulture)} días más que el resto en vender esta carrocería."
            : $"Vendés esta carrocería {Math.Abs(diferencia).ToString("0.#", CultureInfo.InvariantCulture)} días más rápido que el resto.";
    }

    private static string LecturaDeConsultas(double? mio, double mercado)
    {
        if (mio is not { } propio)
        {
            return $"El resto del mercado recibe {mercado.ToString("0.#", CultureInfo.InvariantCulture)} consultas "
                   + "cada cien visitas. Todavía no tenés visitas suficientes para comparar.";
        }

        if (Math.Abs(propio - mercado) < 1)
        {
            return "Convertís visitas en consultas igual que el resto del mercado.";
        }

        return propio > mercado
            ? "Convertís mejor que el resto: de cada cien visitas te consultan más que al promedio."
            : "Convertís por debajo del promedio. Suele ser precio, fotos o la descripción.";
    }
}
