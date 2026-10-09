using System.Globalization;
using System.Text.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Reportes;
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

    /// <summary>Días de historia del precio de referencia de mercado.</summary>
    private const int DiasDeSnapshots = 7;

    /// <summary>
    /// Tipo de cambio de referencia para llevar a dólares los avisos en pesos al calcular
    /// la mediana del mercado. Es el mismo valor con el que arranca la serie de
    /// cotizaciones, así que las dos partes del seed hablan de la misma moneda.
    /// </summary>
    private const decimal PesosPorDolar = 40m;

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

        // Con la marca y la carrocería, no solo el id: son lo que después elige la foto
        // que le corresponde a cada modelo.
        var catalogo = await db.Modelos
            .OrderBy(m => m.Id)
            .Select(m => new ModeloDelCatalogo(m.Id, m.Marca!.Nombre + " " + m.Nombre, m.Carroceria))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var modelos = catalogo.Select(m => m.Id).ToList();

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
                vehiculos.Add(NuevoVehiculo(tenantId, catalogo[azar.Next(catalogo.Count)], ahora, azar));
            }
        }

        db.Vehiculos.AddRange(vehiculos);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AgregarFotos(db, vehiculos, catalogo.ToDictionary(m => m.Id), azar);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // El comportamiento se sortea una sola vez y lo comparten las dos partes: los
        // eventos lo usan para generar vistas y consultas, y el precio de referencia para
        // quedar por debajo del aviso en las unidades que están caras. Si cada parte
        // sorteara el suyo, el reporte diría "precio alto" en una unidad que además figura
        // por debajo del mercado, y esa contradicción se lee como un error del sistema.
        var comportamientos = vehiculos.ToDictionary(v => v, _ => SortearComportamiento(azar));

        AgregarEventos(db, vehiculos, comportamientos, ahora, azar);
        AgregarBusquedas(db, tenants, modelos, vehiculos, ahora, azar);
        AgregarCotizaciones(db, ahora, azar);
        AgregarPreciosDeMercado(db, vehiculos, comportamientos, ahora, azar);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Valor aproximado a nuevo, en dólares, según la carrocería.
    /// </summary>
    /// <remarks>
    /// Sortear el precio sin mirar qué vehículo es deja una camioneta a nueve mil dólares
    /// al lado de un hatchback a cuarenta mil. En una demo a alguien que vive de comprar y
    /// vender autos, eso se nota en la primera pantalla y hace dudar de todo lo demás.
    /// También rompe el precio de referencia: dos unidades del mismo modelo y año con
    /// precios sorteados a ciegas quedan a un cuarenta por ciento una de la otra, y el
    /// reporte muestra un desvío contra el mercado que ningún mercado tiene.
    /// </remarks>
    private static decimal ValorANuevo(Carroceria carroceria) => carroceria switch
    {
        Carroceria.Hatchback => 19_000m,
        Carroceria.Sedan => 23_000m,
        Carroceria.Suv => 32_000m,
        Carroceria.Pickup => 38_000m,
        Carroceria.Coupe => 34_000m,
        Carroceria.Wagon => 26_000m,
        Carroceria.Van => 24_000m,
        Carroceria.Minivan => 25_000m,
        Carroceria.Convertible => 40_000m,
        _ => 22_000m,
    };

    private static Vehiculo NuevoVehiculo(int tenantId, ModeloDelCatalogo modelo, DateTime ahora, Random azar)
    {
        var publicacion = ahora.AddDays(-azar.Next(1, DiasDeHistoria)).Date;
        var estado = SortearEstado(azar);
        var enDolares = azar.Next(10) < 8; // el mercado uruguayo publica casi todo en USD

        var anio = ahora.Year - azar.Next(0, 12);
        var edad = Math.Max(0, ahora.Year - anio);

        // Un usado pierde alrededor de un ocho por ciento por año y se estaciona: un auto
        // de doce años no vale el diez por ciento de uno nuevo, vale cerca de un tercio.
        var depreciacion = Math.Max(0.34m, (decimal)Math.Pow(0.92, edad));

        // El resto es el estado de cada unidad concreta: kilómetros, service, detalles.
        var propio = 0.92m + (azar.Next(0, 17) / 100m);

        var precioUsd = Math.Round(ValorANuevo(modelo.Carroceria) * depreciacion * propio / 100m, 0) * 100m;

        var vehiculo = new Vehiculo
        {
            TenantId = tenantId,
            ModeloId = modelo.Id,
            Anio = anio,

            // Los kilómetros acompañan a la edad: unos quince mil por año, con dispersión.
            Kilometraje = Math.Min(320_000, (edad * azar.Next(9, 22) * 1000) + azar.Next(0, 9) * 1000),
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

    /// <summary>El modelo del catálogo, con lo que hace falta para elegirle una foto.</summary>
    private sealed record ModeloDelCatalogo(int Id, string Clave, Carroceria Carroceria);

    /// <summary>
    /// Le pone a cada vehículo las fotos del modelo que realmente es.
    /// </summary>
    /// <remarks>
    /// El seed no sube binarios a ningún storage: las fotos son referencias a archivos de
    /// licencia libre, elegidas por modelo en <see cref="FotosDeCatalogo"/>. Que la foto
    /// coincida con el modelo no es cosmética — un catálogo donde la Hilux se ve como un
    /// hatchback no permite juzgar ninguna de las pantallas que se apoyan en él.
    /// </remarks>
    private static void AgregarFotos(
        AppDbContext db,
        IReadOnlyList<Vehiculo> vehiculos,
        IReadOnlyDictionary<int, ModeloDelCatalogo> catalogo,
        Random azar)
    {
        foreach (var vehiculo in vehiculos)
        {
            var modelo = catalogo[vehiculo.ModeloId];
            var fotos = FotosDeCatalogo.Para(modelo.Clave, modelo.Carroceria, azar);

            for (var orden = 0; orden < fotos.Length; orden++)
            {
                db.VehiculoFotos.Add(new VehiculoFoto
                {
                    VehiculoId = vehiculo.Id,
                    Url = fotos[orden].Url,
                    UrlThumb = fotos[orden].UrlMiniatura,
                    Orden = orden,
                    EsPortada = orden == 0,
                });
            }
        }
    }

    /// <summary>
    /// Cómo se comporta una unidad: cuánta gente la mira y qué proporción termina
    /// preguntando.
    /// </summary>
    /// <param name="Vistas">Vistas de ficha en los noventa días.</param>
    /// <param name="ConsultasPorCien">Consultas cada cien vistas.</param>
    /// <param name="EstaCara">
    /// Si la unidad está por encima de lo que pide el mercado. Es lo que explica que la
    /// miren mucho y no pregunte nadie, y lo que después tiene que verse también en la
    /// comparación contra el precio de referencia.
    /// </param>
    private sealed record Comportamiento(int Vistas, int ConsultasPorCien, bool EstaCara = false);

    /// <summary>
    /// Sortea el comportamiento de una unidad.
    /// </summary>
    /// <remarks>
    /// Un patio real no es uniforme, y el reporte de demanda existe justamente para separar
    /// lo que anda de lo que no. Si todas las unidades del seed convirtieran igual, todas
    /// darían "saludable" y la pantalla no diría nada: la única forma de ver si la lectura
    /// funciona sería esperar meses de datos reales.
    /// <para>
    /// Las proporciones son las que se ven en un stock de verdad: la mayoría anda bien,
    /// una de cada seis está cara —la mira mucha gente y no pregunta nadie—, una de cada
    /// ocho no la ve nadie porque quedó mal publicada, y algunas llevan meses sin una sola
    /// consulta. Cada caso es el que dispara una señal distinta en
    /// <see cref="UmbralesDeDemanda.Clasificar"/>.
    /// </para>
    /// </remarks>
    private static Comportamiento SortearComportamiento(Random azar)
    {
        var suerte = azar.Next(100);

        return suerte switch
        {
            // Cara: tráfico de sobra y casi nadie pregunta. Es la señal más valiosa del
            // reporte, porque es la que se corrige bajando el precio.
            < 16 => new Comportamiento(azar.Next(45, 160), azar.Next(0, 3), EstaCara: true),

            // Mal publicada: sin fotos buenas o sin destacar, no la ve nadie. El problema
            // no es el precio, y confundirlos hace perder plata.
            < 29 => new Comportamiento(azar.Next(1, 6), azar.Next(0, 10)),

            // Muerta: lleva meses sin una sola consulta.
            < 37 => new Comportamiento(azar.Next(8, 40), 0),

            // La que se lleva la atención del patio.
            < 47 => new Comportamiento(azar.Next(90, 220), azar.Next(6, 14)),

            // El grueso del stock, andando normal.
            _ => new Comportamiento(azar.Next(20, 70), azar.Next(4, 12)),
        };
    }

    /// <summary>
    /// Genera vistas y consultas con una forma parecida a la real: la mayoría de las
    /// unidades junta poco tráfico y unas pocas se llevan casi todo, y las consultas son
    /// una fracción chica de las vistas.
    /// </summary>
    private static void AgregarEventos(
        AppDbContext db,
        IReadOnlyList<Vehiculo> vehiculos,
        IReadOnlyDictionary<Vehiculo, Comportamiento> comportamientos,
        DateTime ahora,
        Random azar)
    {
        foreach (var vehiculo in vehiculos)
        {
            var diasPublicado = Math.Max(1, (int)(ahora - vehiculo.FechaPublicacion).TotalDays);
            var comportamiento = comportamientos[vehiculo];

            for (var i = 0; i < comportamiento.Vistas; i++)
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

                if (azar.Next(100) < comportamiento.ConsultasPorCien)
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
    private static void AgregarBusquedas(
        AppDbContext db,
        IReadOnlyList<int> tenants,
        IReadOnlyList<int> modelos,
        IReadOnlyList<Vehiculo> vehiculos,
        DateTime ahora,
        Random azar)
    {
        foreach (var tenantId in tenants)
        {
            var cuantas = azar.Next(40, 90);
            var calientes = ModelosCalientes(tenantId, modelos, vehiculos, azar);

            for (var i = 0; i < cuantas; i++)
            {
                var cuando = ahora.AddDays(-azar.Next(0, DiasDeHistoria)).AddHours(azar.Next(8, 23));

                // Lo que se pide mucho es también lo que más veces no está.
                var caliente = azar.Next(100) < 55;
                var sinResultado = caliente ? azar.Next(10) < 7 : azar.Next(5) == 0;

                var filtros = caliente
                    ? FiltrosDeModeloCaliente(calientes[azar.Next(calientes.Count)], azar)
                    : FiltrosSinteticos(modelos, azar);

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

    /// <summary>
    /// Una cotización del dólar por día de historia.
    /// </summary>
    /// <remarks>
    /// En producción las pone el cron. Acá hacen falta igual, porque sin ellas el reporte
    /// de demanda no puede comparar un aviso en pesos contra un precio de referencia en
    /// dólares, y en desarrollo esa mitad de la flota se ve sin referencia por una razón
    /// —falta el seed— que no es la que el reporte quiere expresar.
    /// </remarks>
    private static void AgregarCotizaciones(AppDbContext db, DateTime ahora, Random azar)
    {
        var hoy = DateOnly.FromDateTime(ahora);

        // Arranca cerca del valor real del peso uruguayo y camina de a poco, como se mueve
        // de verdad: una serie que salta mil pesos entre dos días haría que cualquier
        // conversión histórica se vea como un error de la aplicación.
        var valor = 40m;

        for (var dias = DiasDeHistoria; dias >= 0; dias--)
        {
            valor += (azar.Next(-20, 21)) / 100m;

            db.Cotizaciones.Add(new Cotizacion
            {
                Fecha = hoy.AddDays(-dias),
                UsdUyu = Math.Round(valor, 4),
            });
        }
    }

    /// <summary>
    /// Precio de referencia de mercado para los modelos y años que hay en stock.
    /// </summary>
    /// <remarks>
    /// En producción estos snapshots los postea el cron con lo que se está pidiendo en
    /// MercadoLibre. En desarrollo hacen falta igual: sin ellos la columna de precio de
    /// referencia queda vacía en todo el reporte, y entonces no se puede ver si la
    /// comparación funciona hasta tener credenciales de MercadoLibre y esperar a que corra
    /// el job.
    /// <para>
    /// La referencia se ancla al precio mediano de lo que hay publicado de ese modelo y
    /// año, y se le aplica un desvío. Anclarla es lo que la hace plausible; desviarla es
    /// lo que hace que el reporte sirva: si la referencia fuera exactamente el precio de
    /// cada unidad, todas darían "en precio" y la pantalla no diría nada. Con el desvío,
    /// unas quedan por encima del mercado y otras por debajo, que es la lectura que el
    /// reporte existe para dar.
    /// </para>
    /// <para>
    /// Un snapshot por día de la última semana y no uno solo: el reporte toma el más
    /// reciente, pero la tabla guarda historia, y un seed que escribe una sola fila no
    /// ejercita esa parte.
    /// </para>
    /// </remarks>
    private static void AgregarPreciosDeMercado(
        AppDbContext db,
        IReadOnlyList<Vehiculo> vehiculos,
        IReadOnlyDictionary<Vehiculo, Comportamiento> comportamientos,
        DateTime ahora,
        Random azar)
    {
        // Solo lo que se ve en el sitio: son las unidades que el reporte de demanda mira.
        var publicados = vehiculos
            .Where(v => v.Estado is EstadoVehiculo.Disponible or EstadoVehiculo.Reservado)
            .ToList();

        var hoy = DateOnly.FromDateTime(ahora);

        foreach (var grupo in publicados.GroupBy(v => (v.ModeloId, v.Anio)))
        {
            // Todo en dólares, que es la moneda en la que se publica y se compara acá. Los
            // avisos en pesos los convierte el reporte con la cotización del día del
            // snapshot.
            var enDolares = grupo
                .Select(v => v.Moneda == Moneda.Usd ? v.Precio : v.Precio / PesosPorDolar)
                .OrderBy(p => p)
                .ToList();

            var mediana = enDolares[enDolares.Count / 2];

            // El desvío de la referencia respecto del stock. En las unidades que están
            // caras la referencia queda claramente por debajo, que es lo que hace que el
            // reporte muestre la misma lectura por los dos lados: mucha gente que mira y no
            // pregunta, y un precio por encima del de mercado.
            var hayAlgunaCara = grupo.Any(v => comportamientos[v].EstaCara);

            var desvio = hayAlgunaCara
                ? 1m - (azar.Next(8, 20) / 100m)
                : 1m + (azar.Next(-6, 7) / 100m);
            var referencia = Math.Round(mediana * desvio, 0);

            for (var dias = DiasDeSnapshots - 1; dias >= 0; dias--)
            {
                // El mercado se mueve poco de un día para otro. Una serie que salta un
                // diez por ciento diario haría que la historia se lea como un error.
                var delDia = Math.Round(referencia * (1m + (azar.Next(-15, 16) / 1000m)), 0);

                db.PreciosDeMercado.Add(new PrecioDeMercado
                {
                    ModeloId = grupo.Key.ModeloId,
                    Anio = grupo.Key.Anio,
                    Moneda = Moneda.Usd,
                    PrecioMediano = delDia,
                    PrecioMinimo = Math.Round(delDia * 0.82m, 0),
                    PrecioMaximo = Math.Round(delDia * 1.24m, 0),
                    Publicaciones = azar.Next(6, 40),
                    Fuente = "MercadoLibre",
                    Fecha = hoy.AddDays(-dias),
                    CreatedAt = ahora.AddDays(-dias),
                });
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

    /// <summary>
    /// Los filtros de una búsqueda inventada, con la misma forma con la que los guarda el
    /// sitio público.
    /// </summary>
    /// <remarks>
    /// Con las opciones web —camelCase— y no con las de por defecto, porque es el mismo
    /// JSON que después leen los reportes. Un seed que escribe las claves de otra manera
    /// hace que los reportes se vean bien en desarrollo por motivos que no se repiten en
    /// producción, que es la peor forma de tener datos de prueba.
    /// <para>
    /// Cuatro de cada diez búsquedas nombran un modelo concreto. Es lo que hace que la
    /// demanda insatisfecha se pueda leer como "te están pidiendo este modelo" en vez de
    /// como "alguien quería una camioneta": sin modelo, el reporte no dice qué comprar.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Los modelos que en esta automotora se piden mucho, con el año desde el que los
    /// piden.
    /// </summary>
    /// <remarks>
    /// La demanda real se concentra: en un mercado chico, mucha gente busca las mismas
    /// tres o cuatro cosas. Repartir las búsquedas uniformemente entre sesenta modelos
    /// —que es lo que hacía este seed— no produce ninguna combinación repetida, y entonces
    /// nada llega al mínimo de visitas distintas que una sugerencia de compra necesita.
    /// El resultado es una pantalla de sugerencias vacía por un artefacto del seed y no
    /// porque el reporte esté mal.
    /// <para>
    /// Se eligen sobre todo modelos que esta automotora no tiene, que es lo que produce la
    /// sugerencia de comprar, y uno que sí tiene, para que también se vea la otra lectura:
    /// te lo piden, lo tenés, y algo del precio o de la publicación no está funcionando.
    /// </para>
    /// </remarks>
    private static List<(int ModeloId, int AnioDesde)> ModelosCalientes(
        int tenantId,
        IReadOnlyList<int> modelos,
        IReadOnlyList<Vehiculo> vehiculos,
        Random azar)
    {
        var enStock = vehiculos
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ModeloId)
            .Distinct()
            .ToList();

        var faltantes = modelos.Where(m => !enStock.Contains(m)).ToList();
        var elegidos = new List<int>();

        for (var i = 0; i < 3 && faltantes.Count > 0; i++)
        {
            var indice = azar.Next(faltantes.Count);
            elegidos.Add(faltantes[indice]);
            faltantes.RemoveAt(indice);
        }

        if (enStock.Count > 0)
        {
            elegidos.Add(enStock[azar.Next(enStock.Count)]);
        }

        return elegidos.Select(m => (m, 2015 + azar.Next(0, 6))).ToList();
    }

    /// <summary>
    /// Los filtros de una búsqueda de un modelo que se pide mucho.
    /// </summary>
    /// <remarks>
    /// Sin carrocería y con el mismo año y presupuesto para todas las búsquedas del mismo
    /// modelo: es lo que hace que varias visitas distintas caigan en el mismo grupo del
    /// reporte. Quien busca un modelo concreto no filtra además por carrocería —el modelo
    /// ya la determina— y los presupuestos se agrupan en cifras redondas.
    /// </remarks>
    private static string FiltrosDeModeloCaliente((int ModeloId, int AnioDesde) caliente, Random azar)
        => JsonSerializer.Serialize(
            new
            {
                caliente.ModeloId,
                AnioDesde = caliente.AnioDesde,
                Moneda = "Usd",
                PrecioHasta = azar.Next(2, 5) * 5000,
            },
            OpcionesDeFiltros);

    private static string FiltrosSinteticos(IReadOnlyList<int> modelos, Random azar)
    {
        var carroceria = Enum.GetValues<Carroceria>()[azar.Next(Enum.GetValues<Carroceria>().Length)];
        var conModelo = azar.Next(10) < 4;

        return JsonSerializer.Serialize(
            new
            {
                ModeloId = conModelo ? modelos[azar.Next(modelos.Count)] : (int?)null,
                Carroceria = conModelo ? null : carroceria.ToString(),
                AnioDesde = 2015 + azar.Next(0, 8),
                Moneda = "Usd",
                PrecioHasta = azar.Next(10, 40) * 1000,
            },
            OpcionesDeFiltros);
    }

    /// <summary>Las mismas con las que el sitio público serializa los filtros.</summary>
    private static readonly JsonSerializerOptions OpcionesDeFiltros = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

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
