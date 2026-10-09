using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Publico;

namespace AutomotoraSaaS.Core.Vehiculos;

/// <summary>
/// Proyección de la entidad a los DTOs que salen por la API.
/// </summary>
/// <remarks>
/// Se mapea en memoria, sobre entidades ya materializadas, y no con un <c>Select</c> que
/// EF traduzca a SQL. Los días en góndola necesitan la hora actual, y todas las formas de
/// calcular una diferencia de fechas dentro de una consulta dependen del proveedor: lo
/// que traduce Pomelo no lo traduce SQLite, y los tests corren sobre SQLite justamente
/// para que el SQL sea real. Con páginas de hasta 60 vehículos, traer las columnas de más
/// no se nota; que el mismo código ande contra los dos proveedores, sí.
/// </remarks>
public static class MapeosDeVehiculo
{
    /// <summary>
    /// Días que la unidad lleva —o llevó— publicada. Es la métrica que dice si el precio
    /// está bien puesto, así que se corta en cero: un número negativo por una fecha mal
    /// cargada ensuciaría el promedio de todos.
    /// </summary>
    public static int DiasEnGondola(DateTime fechaPublicacion, DateTime? fechaVenta, DateTime ahora)
    {
        var hasta = fechaVenta ?? ahora;
        var dias = (int)(hasta.Date - fechaPublicacion.Date).TotalDays;

        return dias < 0 ? 0 : dias;
    }

    public static VehiculoDto ADto(this Vehiculo vehiculo, DateTime ahora, bool incluirPrecioCosto)
    {
        var modelo = ModeloDe(vehiculo);
        var marca = MarcaDe(modelo);

        return new VehiculoDto(
            vehiculo.Id,
            marca.Id,
            marca.Nombre,
            modelo.Id,
            modelo.Nombre,
            vehiculo.VersionId,
            vehiculo.Version?.Nombre,
            modelo.Carroceria.ToString(),
            vehiculo.Anio,
            vehiculo.Kilometraje,
            vehiculo.Combustible.ToString(),
            vehiculo.Transmision.ToString(),
            vehiculo.Color,
            vehiculo.Puertas,
            vehiculo.Motor,
            vehiculo.Precio,
            vehiculo.Moneda.ToString(),
            vehiculo.Estado.ToString(),
            vehiculo.Descripcion,
            vehiculo.Destacado,
            // El Seller no ve el costo. Va en null desde el servidor, no oculto en la UI.
            incluirPrecioCosto ? vehiculo.PrecioCosto : null,
            vehiculo.FechaPublicacion,
            vehiculo.FechaVenta,
            vehiculo.PrecioVenta,
            DiasEnGondola(vehiculo.FechaPublicacion, vehiculo.FechaVenta, ahora),
            Galeria(vehiculo),
            vehiculo.CreatedAt,
            vehiculo.UpdatedAt);
    }

    public static VehiculoResumenDto AResumen(this Vehiculo vehiculo, DateTime ahora)
    {
        var modelo = ModeloDe(vehiculo);

        return new VehiculoResumenDto(
            vehiculo.Id,
            MarcaDe(modelo).Nombre,
            modelo.Nombre,
            vehiculo.Version?.Nombre,
            vehiculo.Anio,
            vehiculo.Kilometraje,
            vehiculo.Precio,
            vehiculo.Moneda.ToString(),
            vehiculo.Estado.ToString(),
            vehiculo.Destacado,
            PortadaParaGrilla(vehiculo),
            DiasEnGondola(vehiculo.FechaPublicacion, vehiculo.FechaVenta, ahora),
            vehiculo.FechaPublicacion);
    }

    public static VehiculoPublicoResumenDto AResumenPublico(this Vehiculo vehiculo)
    {
        var modelo = ModeloDe(vehiculo);

        return new VehiculoPublicoResumenDto(
            vehiculo.Id,
            MarcaDe(modelo).Nombre,
            modelo.Nombre,
            vehiculo.Version?.Nombre,
            modelo.Carroceria.ToString(),
            vehiculo.Anio,
            vehiculo.Kilometraje,
            vehiculo.Precio,
            vehiculo.Moneda.ToString(),
            vehiculo.Combustible.ToString(),
            vehiculo.Transmision.ToString(),
            PortadaParaGrilla(vehiculo),
            vehiculo.Destacado);
    }

    public static VehiculoPublicoDto ADtoPublico(this Vehiculo vehiculo, string nombreDeLaAutomotora)
    {
        var modelo = ModeloDe(vehiculo);
        var marca = MarcaDe(modelo);
        var titulo = $"{marca.Nombre} {modelo.Nombre} {vehiculo.Anio}";

        return new VehiculoPublicoDto(
            vehiculo.Id,
            marca.Nombre,
            modelo.Nombre,
            vehiculo.Version?.Nombre,
            modelo.Carroceria.ToString(),
            vehiculo.Anio,
            vehiculo.Kilometraje,
            vehiculo.Combustible.ToString(),
            vehiculo.Transmision.ToString(),
            vehiculo.Color,
            vehiculo.Puertas,
            vehiculo.Motor,
            vehiculo.Precio,
            vehiculo.Moneda.ToString(),
            vehiculo.Descripcion,
            vehiculo.Destacado,
            Galeria(vehiculo),
            $"{titulo} — {nombreDeLaAutomotora}",
            $"Hola, me interesa el {titulo} que vi en la web.");
    }

    public static VehiculoFotoDto ADto(this VehiculoFoto foto)
    {
        ArgumentNullException.ThrowIfNull(foto);

        return new VehiculoFotoDto(foto.Id, foto.Url, foto.UrlThumb, foto.Orden, foto.EsPortada);
    }

    /// <summary>
    /// La portada, o la primera de la galería si ninguna quedó marcada. Que un vehículo
    /// aparezca sin foto porque nadie tildó la portada sería un bug caro y silencioso.
    /// </summary>
    public static VehiculoFoto? Portada(Vehiculo vehiculo)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        return vehiculo.Fotos.FirstOrDefault(f => f.EsPortada)
               ?? vehiculo.Fotos.OrderBy(f => f.Orden).FirstOrDefault();
    }

    /// <summary>
    /// La portada en tamaño de grilla: la miniatura si existe, y si no la imagen entera.
    /// </summary>
    /// <remarks>
    /// Un listado son doce tarjetas del tamaño de una estampilla. Servirlas con la imagen
    /// de ficha es bajar varios megabytes para pintar unos pocos miles de píxeles, y el
    /// costo se lo lleva entero el comprador que entra desde el celular con datos —que es
    /// la mayoría del tráfico. La ficha sí usa la imagen grande: ahí se mira.
    /// <para>
    /// Con caída a <see cref="VehiculoFoto.Url"/> porque la miniatura es opcional: una foto
    /// vieja, o subida por un camino que no la generó, no puede dejar la tarjeta en blanco.
    /// </para>
    /// </remarks>
    public static string? PortadaParaGrilla(Vehiculo vehiculo)
    {
        var portada = Portada(vehiculo);

        return portada is null ? null : portada.UrlThumb ?? portada.Url;
    }

    private static IReadOnlyList<VehiculoFotoDto> Galeria(Vehiculo vehiculo)
        => vehiculo.Fotos
            .OrderByDescending(f => f.EsPortada)
            .ThenBy(f => f.Orden)
            .Select(f => f.ADto())
            .ToList();

    // Llegar acá sin las navegaciones cargadas es un bug de la consulta, no una entrada
    // del usuario: mejor reventar con el motivo escrito que devolver un vehículo sin marca.
    private static Modelo ModeloDe(Vehiculo vehiculo)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        return vehiculo.Modelo
               ?? throw new InvalidOperationException(
                   $"El vehículo {vehiculo.Id} se proyectó sin su modelo cargado. Falta un Include.");
    }

    private static Marca MarcaDe(Modelo modelo)
        => modelo.Marca
           ?? throw new InvalidOperationException(
               $"El modelo {modelo.Id} se proyectó sin su marca cargada. Falta un ThenInclude.");
}
