using System.Globalization;
using System.Text;
using System.Text.Json;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Reportes;

/// <summary>
/// Los reportes de demanda.
/// </summary>
/// <remarks>
/// Todo lo que hay acá se apoya en eventos que se registran desde antes de que existiera
/// un solo reporte. Ese orden no fue casualidad: los datos de demanda solo valen
/// acumulados en el tiempo, y lo que no se midió no se recupera nunca.
/// <para>
/// El tenant no aparece por ningún lado: lo pone el filtro global del <c>DbContext</c>.
/// </para>
/// </remarks>
public sealed class ServicioDeReportes : IServicioDeReportes
{
    private const int CombinacionesDeDemanda = 15;

    private static readonly TipoEvento[] EventosDeConsulta =
        [TipoEvento.ClickWhatsapp, TipoEvento.ClickTelefono];

    private static readonly JsonSerializerOptions LecturaDeFiltros = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly TimeProvider _reloj;

    public ServicioDeReportes(AppDbContext db, TimeProvider reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    public async Task<ReporteDeDemandaDto> DemandaAsync(int dias, CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var desde = ahora.AddDays(-dias);

        var vehiculos = await AnalizarGondolaAsync(desde, ahora, cancellationToken).ConfigureAwait(false);
        var pedidos = await AgruparBusquedasFallidasAsync(desde, cancellationToken).ConfigureAwait(false);

        var insatisfecha = pedidos
            .OrderByDescending(p => p.Veces)
            .ThenByDescending(p => p.UltimaVez)
            .Take(CombinacionesDeDemanda)
            .Select(p => new DemandaInsatisfechaDto(
                p.Marca,
                p.Modelo,
                p.Carroceria,
                p.Combustible,
                p.Transmision,
                p.AnioDesde,
                p.PrecioHasta,
                p.Moneda,
                p.Veces,
                p.UltimaVez,
                p.Descripcion))
            .ToList();

        return new ReporteDeDemandaDto(
            dias,
            vehiculos.Sum(v => v.Vistas),
            vehiculos.Sum(v => v.Consultas),
            vehiculos,
            insatisfecha);
    }

    /// <summary>
    /// Cruza lo que la gente buscó y no encontró con lo rápido que esta automotora vende
    /// cosas parecidas.
    /// </summary>
    /// <remarks>
    /// La demanda sola dice qué quieren; la rotación dice si conviene. Un modelo muy
    /// buscado que después queda seis meses en el patio no es una buena compra, y ese dato
    /// solo lo tiene la propia automotora en su historial de ventas.
    /// </remarks>
    public async Task<IReadOnlyList<SugerenciaDeCompraDto>> SugerenciasDeCompraAsync(
        int dias,
        CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var desde = ahora.AddDays(-dias);

        var pedidos = await AgruparBusquedasFallidasAsync(desde, cancellationToken).ConfigureAwait(false);

        var candidatos = pedidos
            .Where(p => p.Veces >= UmbralesDeSugerencia.BusquedasMinimas)
            .OrderByDescending(p => p.Veces)
            .ThenByDescending(p => p.UltimaVez)
            .Take(UmbralesDeSugerencia.Maximo)
            .ToList();

        if (candidatos.Count == 0)
        {
            return [];
        }

        var rotacion = await CalcularRotacionAsync(cancellationToken).ConfigureAwait(false);

        return candidatos
            .Select(pedido =>
            {
                var historial = RotacionDe(pedido, rotacion);

                return new SugerenciaDeCompraDto(
                    pedido.Descripcion,
                    Fundamentar(pedido, historial, dias),
                    pedido.Veces,
                    pedido.UltimaVez,
                    pedido.Marca,
                    pedido.Modelo,
                    pedido.Carroceria,
                    pedido.AnioDesde,
                    pedido.PrecioHasta,
                    pedido.Moneda,
                    historial?.Unidades,
                    historial?.DiasPromedio);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<VehiculoEnGondolaDto>> AnalizarGondolaAsync(
        DateTime desde,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var publicados = await _db.Vehiculos
            .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
            .Include(v => v.Version)
            .Include(v => v.Fotos)
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (publicados.Count == 0)
        {
            return [];
        }

        var conteos = await _db.Eventos
            .Where(e => e.CreatedAt >= desde && e.VehiculoId != null)
            .GroupBy(e => new { VehiculoId = e.VehiculoId!.Value, e.Tipo })
            .Select(g => new { g.Key.VehiculoId, g.Key.Tipo, Cantidad = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vistas = conteos
            .Where(c => c.Tipo == TipoEvento.ViewFicha)
            .ToDictionary(c => c.VehiculoId, c => c.Cantidad);

        var consultas = conteos
            .Where(c => EventosDeConsulta.Contains(c.Tipo))
            .GroupBy(c => c.VehiculoId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Cantidad));

        return publicados
            .Select(vehiculo =>
            {
                var modelo = vehiculo.Modelo!;
                var mirado = vistas.GetValueOrDefault(vehiculo.Id);
                var preguntado = consultas.GetValueOrDefault(vehiculo.Id);
                var enGondola = MapeosDeVehiculo.DiasEnGondola(vehiculo.FechaPublicacion, null, ahora);
                var ratio = mirado == 0 ? 0 : Math.Round(preguntado * 100d / mirado, 1);
                var senal = Clasificar(mirado, ratio, enGondola);

                return new VehiculoEnGondolaDto(
                    vehiculo.Id,
                    modelo.Marca!.Nombre,
                    modelo.Nombre,
                    vehiculo.Version?.Nombre,
                    vehiculo.Anio,
                    vehiculo.Precio,
                    vehiculo.Moneda.ToString(),
                    vehiculo.Estado.ToString(),
                    MapeosDeVehiculo.Portada(vehiculo)?.Url,
                    enGondola,
                    mirado,
                    preguntado,
                    ratio,
                    senal.ToString(),
                    Explicar(senal, mirado, preguntado, enGondola));
            })
            // Lo que necesita decisión primero, y dentro de eso lo que lleva más tiempo parado.
            .OrderBy(v => Prioridad(v.Senal))
            .ThenByDescending(v => v.DiasEnGondola)
            .ToList();
    }

    /// <summary>
    /// Agrupa las búsquedas que no encontraron nada por la combinación de filtros pedida.
    /// </summary>
    /// <remarks>
    /// Los filtros se guardaron como JSON y se agrupan en memoria, a propósito: la
    /// alternativa es que cada motor los sepa consultar por adentro, y eso ata la
    /// analítica —que es el producto— a la base. Con el volumen de una automotora la
    /// diferencia no se nota.
    /// </remarks>
    private async Task<IReadOnlyList<PedidoAgrupado>> AgruparBusquedasFallidasAsync(
        DateTime desde,
        CancellationToken cancellationToken)
    {
        var sinResultado = await _db.Busquedas
            .Where(b => b.ResultadosCount == 0 && b.CreatedAt >= desde)
            .Select(b => new { b.Filtros, b.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sinResultado.Count == 0)
        {
            return [];
        }

        var pedidos = sinResultado
            .Select(b => new { Filtros = Leer(b.Filtros), b.CreatedAt })
            .Where(b => b.Filtros is not null)
            .ToList();

        var marcas = await _db.Marcas
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken)
            .ConfigureAwait(false);

        var modelos = await _db.Modelos
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken)
            .ConfigureAwait(false);

        return pedidos
            .GroupBy(p => new
            {
                p.Filtros!.MarcaId,
                p.Filtros.ModeloId,
                p.Filtros.Carroceria,
                p.Filtros.Combustible,
                p.Filtros.Transmision,
                p.Filtros.AnioDesde,
                p.Filtros.PrecioHasta,
                p.Filtros.Moneda,
            })
            .Select(g =>
            {
                var marca = g.Key.MarcaId is { } marcaId ? marcas.GetValueOrDefault(marcaId) : null;
                var modelo = g.Key.ModeloId is { } modeloId ? modelos.GetValueOrDefault(modeloId) : null;

                return new PedidoAgrupado(
                    g.Key.ModeloId,
                    g.Key.Carroceria,
                    marca,
                    modelo,
                    g.Key.Combustible,
                    g.Key.Transmision,
                    g.Key.AnioDesde,
                    g.Key.PrecioHasta,
                    g.Key.Moneda,
                    g.Count(),
                    g.Max(p => p.CreatedAt),
                    Describir(
                        marca,
                        modelo,
                        g.Key.Carroceria,
                        g.Key.Combustible,
                        g.Key.Transmision,
                        g.Key.AnioDesde,
                        g.Key.PrecioHasta,
                        g.Key.Moneda));
            })
            .ToList();
    }

    /// <summary>
    /// Cuánto tardó esta automotora en vender lo que ya vendió, por modelo y por
    /// carrocería.
    /// </summary>
    private async Task<Rotacion> CalcularRotacionAsync(CancellationToken cancellationToken)
    {
        var vendidos = await _db.Vehiculos
            .Where(v => v.Estado == EstadoVehiculo.Vendido && v.FechaVenta != null)
            .Select(v => new
            {
                v.ModeloId,
                Carroceria = v.Modelo!.Carroceria,
                v.FechaPublicacion,
                FechaVenta = v.FechaVenta!.Value,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var conDias = vendidos
            .Select(v => new
            {
                v.ModeloId,
                v.Carroceria,
                Dias = MapeosDeVehiculo.DiasEnGondola(v.FechaPublicacion, v.FechaVenta, v.FechaVenta),
            })
            .ToList();

        return new Rotacion(
            conDias
                .GroupBy(v => v.ModeloId)
                .ToDictionary(g => g.Key, g => new HistorialDeVentas(g.Count(), (int)Math.Round(g.Average(v => v.Dias)))),
            conDias
                .GroupBy(v => v.Carroceria)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => new HistorialDeVentas(g.Count(), (int)Math.Round(g.Average(v => v.Dias))),
                    StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// El historial más específico que exista: primero el del modelo pedido, si no el de
    /// la carrocería. Nulo si no hay ventas suficientes para hablar de promedios.
    /// </summary>
    private static HistorialDeVentas? RotacionDe(PedidoAgrupado pedido, Rotacion rotacion)
    {
        if (pedido.ModeloId is { } modeloId
            && rotacion.PorModelo.TryGetValue(modeloId, out var porModelo)
            && porModelo.Unidades >= UmbralesDeSugerencia.VentasMinimasParaRotacion)
        {
            return porModelo;
        }

        if (pedido.Carroceria is { } carroceria
            && rotacion.PorCarroceria.TryGetValue(carroceria, out var porCarroceria)
            && porCarroceria.Unidades >= UmbralesDeSugerencia.VentasMinimasParaRotacion)
        {
            return porCarroceria;
        }

        return null;
    }

    private static string Fundamentar(PedidoAgrupado pedido, HistorialDeVentas? historial, int dias)
    {
        var demanda = pedido.Veces == 1
            ? $"Una persona lo buscó y no lo encontró en los últimos {dias} días."
            : $"{pedido.Veces} personas lo buscaron y no lo encontraron en los últimos {dias} días.";

        if (historial is null)
        {
            return $"{demanda} Todavía no vendiste suficientes unidades parecidas como para "
                   + "saber cuánto tardarías en colocarlo.";
        }

        return $"{demanda} Las {historial.Unidades} unidades parecidas que vendiste salieron "
               + $"en {historial.DiasPromedio} días promedio.";
    }

    private static FiltrosRegistrados? Leer(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<FiltrosRegistrados>(json, LecturaDeFiltros);
        }
        catch (JsonException)
        {
            // Una fila con JSON viejo o corrupto se saltea: un reporte incompleto es mucho
            // mejor que un reporte que no se puede abrir.
            return null;
        }
    }

    private static SenalDeDemanda Clasificar(int vistas, double consultasPorCienVistas, int diasEnGondola)
    {
        if (diasEnGondola >= UmbralesDeDemanda.DiasParaEsperarInteres
            && vistas < UmbralesDeDemanda.VistasQueIndicanFaltaDeInteres)
        {
            return SenalDeDemanda.SinInteres;
        }

        if (vistas < UmbralesDeDemanda.VistasMinimasParaConcluir)
        {
            return SenalDeDemanda.PocosDatos;
        }

        return consultasPorCienVistas < UmbralesDeDemanda.ConsultasPorCienVistasBajas
            ? SenalDeDemanda.PrecioAlto
            : SenalDeDemanda.Normal;
    }

    private static string Explicar(SenalDeDemanda senal, int vistas, int consultas, int dias) => senal switch
    {
        SenalDeDemanda.SinInteres =>
            $"Lleva {dias} días publicada y solo {vistas} visitas. El problema es que no la están viendo: "
            + "revisá las fotos, el título y si el modelo tiene demanda.",

        SenalDeDemanda.PrecioAlto =>
            $"{vistas} personas la miraron y solo {consultas} consultaron. Cuando miran y no preguntan, "
            + "casi siempre es el precio.",

        SenalDeDemanda.Normal =>
            $"{consultas} consultas sobre {vistas} visitas. La proporción es la esperable.",

        _ => $"Todavía son {vistas} visitas: hacen falta al menos "
             + $"{UmbralesDeDemanda.VistasMinimasParaConcluir} para poder decir algo.",
    };

    private static int Prioridad(string senal) => senal switch
    {
        nameof(SenalDeDemanda.PrecioAlto) => 0,
        nameof(SenalDeDemanda.SinInteres) => 1,
        nameof(SenalDeDemanda.Normal) => 2,
        _ => 3,
    };

    /// <summary>Arma la frase de lo que buscaron, para no obligar a leer una tabla de filtros.</summary>
    private static string Describir(
        string? marca,
        string? modelo,
        string? carroceria,
        string? combustible,
        string? transmision,
        int? anioDesde,
        decimal? precioHasta,
        string? moneda)
    {
        var frase = new StringBuilder();

        if (marca is not null)
        {
            frase.Append(marca);
        }

        if (modelo is not null)
        {
            frase.Append(frase.Length > 0 ? " " : string.Empty).Append(modelo);
        }

        if (carroceria is not null)
        {
            frase.Append(frase.Length > 0 ? $" ({carroceria})" : carroceria);
        }

        if (frase.Length == 0)
        {
            frase.Append("Cualquier vehículo");
        }

        if (combustible is not null)
        {
            frase.Append(", ").Append(combustible.ToLowerInvariant());
        }

        if (transmision is not null)
        {
            frase.Append(", ").Append(transmision.ToLowerInvariant());
        }

        if (anioDesde is { } anio)
        {
            frase.Append(CultureInfo.InvariantCulture, $", del {anio} en adelante");
        }

        if (precioHasta is { } tope)
        {
            var simbolo = string.Equals(moneda, nameof(Moneda.Usd), StringComparison.OrdinalIgnoreCase)
                ? "US$"
                : "$";

            frase.Append(CultureInfo.InvariantCulture, $", hasta {simbolo} {tope:N0}");
        }

        return frase.ToString();
    }

    /// <summary>Una combinación de filtros pedida y no encontrada, ya resuelta a nombres.</summary>
    private sealed record PedidoAgrupado(
        int? ModeloId,
        string? Carroceria,
        string? Marca,
        string? Modelo,
        string? Combustible,
        string? Transmision,
        int? AnioDesde,
        decimal? PrecioHasta,
        string? Moneda,
        int Veces,
        DateTime UltimaVez,
        string Descripcion);

    private sealed record HistorialDeVentas(int Unidades, int DiasPromedio);

    private sealed record Rotacion(
        IReadOnlyDictionary<int, HistorialDeVentas> PorModelo,
        IReadOnlyDictionary<string, HistorialDeVentas> PorCarroceria);
}
