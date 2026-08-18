using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Storage;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// ABM de vehículos del panel privado.
/// </summary>
/// <remarks>
/// No hay un solo <c>WHERE tenant_id = ...</c> escrito a mano, y no es un descuido: el
/// filtro global recorta las consultas al tenant del token y la política de escritura
/// sella el tenant en las altas. Pedir por id un vehículo de otra automotora no devuelve
/// una fila que después haya que acordarse de descartar: no devuelve nada, y el endpoint
/// responde 404.
/// </remarks>
[ApiController]
[Route("api/vehiculos")]
[Authorize(Policy = Politicas.PanelDeTenant)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class VehiculosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;
    private readonly TimeProvider _reloj;

    public VehiculosController(AppDbContext db, IImageStorage storage, TimeProvider reloj)
    {
        _db = db;
        _storage = storage;
        _reloj = reloj;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    [HttpGet]
    [ProducesResponseType(typeof(PaginaDe<VehiculoResumenDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDe<VehiculoResumenDto>>> Listar(
        [FromQuery] FiltrosDeVehiculos filtros,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtros);

        var pagina = Paginacion.NormalizarPagina(filtros.Pagina);
        var porPagina = Paginacion.NormalizarPorPagina(filtros.PorPagina);

        var consulta = Aplicar(ConVinculos(), filtros);

        var total = await consulta.CountAsync(cancellationToken).ConfigureAwait(false);

        var vehiculos = await consulta
            // Destacados primero y después por publicación: es el orden en que el
            // vendedor quiere ver su stock, no el de los ids.
            .OrderByDescending(v => v.Destacado)
            .ThenByDescending(v => v.FechaPublicacion)
            .ThenByDescending(v => v.Id)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ahora = Ahora;

        return Ok(new PaginaDe<VehiculoResumenDto>(
            vehiculos.Select(v => v.AResumen(ahora)).ToList(), total, pagina, porPagina));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehiculoDto>> Obtener(int id, CancellationToken cancellationToken)
    {
        var vehiculo = await BuscarAsync(id, cancellationToken).ConfigureAwait(false);

        return vehiculo is null ? NoExiste(id) : Ok(ADto(vehiculo));
    }

    [HttpPost]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VehiculoDto>> Crear(
        GuardarVehiculoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await VerificarCatalogoAsync(request, cancellationToken).ConfigureAwait(false) is { } error)
        {
            return error;
        }

        var vehiculo = new Vehiculo
        {
            // El tenant no se escribe a mano ni se acepta del cuerpo: lo sella SaveChanges
            // con el del token.
            Estado = EstadoVehiculo.Disponible,
            FechaPublicacion = request.FechaPublicacion ?? Ahora,
        };

        Volcar(request, vehiculo);

        _db.Vehiculos.Add(vehiculo);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Se recarga con las navegaciones para poder devolver marca y modelo por nombre.
        var creado = await BuscarAsync(vehiculo.Id, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(Obtener), new { id = vehiculo.Id }, ADto(creado!));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehiculoDto>> Actualizar(
        int id,
        GuardarVehiculoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehiculo = await BuscarAsync(id, cancellationToken).ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NoExiste(id);
        }

        if (await VerificarCatalogoAsync(request, cancellationToken).ConfigureAwait(false) is { } error)
        {
            return error;
        }

        Volcar(request, vehiculo);

        if (request.FechaPublicacion is { } publicacion)
        {
            vehiculo.FechaPublicacion = publicacion;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(ADto(await RecargarAsync(id, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>
    /// Cambio rápido de estado. Marcar vendido saca la unidad del sitio público en el acto
    /// y le pone fecha y precio de venta.
    /// </summary>
    [HttpPost("{id:int}/estado")]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehiculoDto>> CambiarEstado(
        int id,
        CambiarEstadoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehiculo = await BuscarAsync(id, cancellationToken).ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NoExiste(id);
        }

        var estado = Enumeraciones.Parsear<EstadoVehiculo>(request.Estado);
        vehiculo.Estado = estado;

        if (estado == EstadoVehiculo.Vendido)
        {
            vehiculo.FechaVenta = request.FechaVenta;
            vehiculo.PrecioVenta = request.PrecioVenta;

            // Un vehículo vendido no puede seguir destacado en la home.
            vehiculo.Destacado = false;
        }
        else
        {
            // Deshacer una venta tiene que limpiar sus datos: si quedaran, el vehículo
            // contaría como vendido en todos los reportes mientras sigue publicado.
            vehiculo.FechaVenta = null;
            vehiculo.PrecioVenta = null;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(ADto(await RecargarAsync(id, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>
    /// Borra el vehículo y sus fotos. Solo el Owner.
    /// </summary>
    /// <remarks>
    /// Es una salida de emergencia para una carga equivocada, no la forma de sacar una
    /// unidad del sitio: para eso está marcarla como vendida, que la saca de la vista y
    /// conserva la historia. Los eventos sobreviven al borrado; su vehículo queda en null.
    /// </remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Politicas.SoloOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Borrar(int id, CancellationToken cancellationToken)
    {
        var vehiculo = await BuscarAsync(id, cancellationToken).ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NoExiste(id);
        }

        var claves = vehiculo.Fotos.Select(ClaveDe).Where(GeneradorDeClaves.EsSegura).ToList();

        _db.Vehiculos.Remove(vehiculo);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Los binarios se borran después de que la fila se fue. Al revés, un fallo al
        // guardar dejaría fichas apuntando a fotos que ya no existen.
        foreach (var clave in claves)
        {
            await _storage.BorrarAsync(clave!, cancellationToken).ConfigureAwait(false);
        }

        return NoContent();
    }

    private IQueryable<Vehiculo> ConVinculos()
        => _db.Vehiculos
            .Include(v => v.Modelo!).ThenInclude(m => m.Marca)
            .Include(v => v.Version)
            .Include(v => v.Fotos);

    private static IQueryable<Vehiculo> Aplicar(IQueryable<Vehiculo> consulta, FiltrosDeVehiculos filtros)
    {
        if (Enumeraciones.ParsearOpcional<EstadoVehiculo>(filtros.Estado) is { } estado)
        {
            consulta = consulta.Where(v => v.Estado == estado);
        }

        if (filtros.MarcaId is { } marcaId)
        {
            consulta = consulta.Where(v => v.Modelo!.MarcaId == marcaId);
        }

        if (filtros.ModeloId is { } modeloId)
        {
            consulta = consulta.Where(v => v.ModeloId == modeloId);
        }

        if (filtros.Destacado is { } destacado)
        {
            consulta = consulta.Where(v => v.Destacado == destacado);
        }

        if (!string.IsNullOrWhiteSpace(filtros.Texto))
        {
            var texto = filtros.Texto.Trim();

            consulta = consulta.Where(v =>
                v.Modelo!.Nombre.Contains(texto)
                || v.Modelo!.Marca!.Nombre.Contains(texto)
                || (v.Version != null && v.Version.Nombre.Contains(texto))
                || (v.Color != null && v.Color.Contains(texto)));
        }

        return consulta;
    }

    private Task<Vehiculo?> BuscarAsync(int id, CancellationToken cancellationToken)
        => ConVinculos().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    private async Task<Vehiculo> RecargarAsync(int id, CancellationToken cancellationToken)
        => await BuscarAsync(id, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException($"El vehículo {id} desapareció mientras se lo editaba.");

    private VehiculoDto ADto(Vehiculo vehiculo) => vehiculo.ADto(Ahora, User.PuedeVerCostos());

    /// <summary>
    /// El precio de costo solo lo escribe quien puede verlo. Si un Seller lo manda, se
    /// ignora en silencio: rechazar el request lo obligaría a saber que el campo existe.
    /// </summary>
    private void Volcar(GuardarVehiculoRequest request, Vehiculo vehiculo)
    {
        vehiculo.ModeloId = request.ModeloId;
        vehiculo.VersionId = request.VersionId;
        vehiculo.Anio = request.Anio;
        vehiculo.Kilometraje = request.Kilometraje;
        vehiculo.Combustible = Enumeraciones.Parsear<Combustible>(request.Combustible);
        vehiculo.Transmision = Enumeraciones.Parsear<Transmision>(request.Transmision);
        vehiculo.Color = Vacio(request.Color);
        vehiculo.Puertas = request.Puertas;
        vehiculo.Motor = Vacio(request.Motor);
        vehiculo.Precio = request.Precio;
        vehiculo.Moneda = Enumeraciones.Parsear<Moneda>(request.Moneda);
        vehiculo.Descripcion = Vacio(request.Descripcion);
        vehiculo.Destacado = request.Destacado;

        if (User.PuedeVerCostos())
        {
            vehiculo.PrecioCosto = request.PrecioCosto;
        }
    }

    /// <summary>
    /// El modelo y la versión tienen que existir, estar activos y corresponderse entre sí.
    /// Es lo que sostiene la normalización: sin esto, el select encadenado se puede saltear
    /// mandando ids sueltos y la analítica se llena de combinaciones que no existen.
    /// </summary>
    private async Task<ActionResult?> VerificarCatalogoAsync(
        GuardarVehiculoRequest request,
        CancellationToken cancellationToken)
    {
        var modeloValido = await _db.Modelos
            .AnyAsync(m => m.Id == request.ModeloId && m.Activo, cancellationToken)
            .ConfigureAwait(false);

        if (!modeloValido)
        {
            return Problem(
                detail: "El modelo no existe o está dado de baja.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.VersionId is not { } versionId)
        {
            return null;
        }

        var versionValida = await _db.Versiones
            .AnyAsync(v => v.Id == versionId && v.ModeloId == request.ModeloId && v.Activo, cancellationToken)
            .ConfigureAwait(false);

        return versionValida
            ? null
            : Problem(
                detail: "La versión no existe, está dada de baja o no es de ese modelo.",
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static string? Vacio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Reconstruye la clave de storage a partir de la URL pública. La alternativa era
    /// agregarle una columna a <c>vehiculo_fotos</c>, y la URL ya lleva la clave adentro.
    /// </summary>
    internal static string? ClaveDe(VehiculoFoto foto)
    {
        var indice = foto.Url.IndexOf("tenants/", StringComparison.Ordinal);

        return indice < 0 ? null : foto.Url[indice..];
    }

    /// <summary>
    /// 404, no 403. El vehículo de otra automotora y el que no existe se responden igual:
    /// distinguirlos convertiría el endpoint en una forma de averiguar qué ids están
    /// ocupados en el resto del sistema.
    /// </summary>
    private ActionResult NoExiste(int id)
        => Problem(detail: $"No existe el vehículo {id}.", statusCode: StatusCodes.Status404NotFound);
}
