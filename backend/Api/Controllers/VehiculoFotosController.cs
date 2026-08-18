using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Storage;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Galería de fotos de un vehículo: subir, borrar, reordenar y elegir portada.
/// </summary>
/// <remarks>
/// Se sube <b>una foto por request</b>, no diez en un multipart gigante. El criterio de
/// aceptación dice que cargar un vehículo con diez fotos desde el celular tiene que andar
/// sin timeout, y un solo request con diez imágenes es exactamente lo que no lo cumple:
/// por 4G tarda, y si se corta a la novena se pierden las nueve. De a una, cada foto
/// muestra progreso propio y un fallo reintenta solo esa.
/// <para>
/// Achicar la imagen es responsabilidad del cliente, antes de subirla. El servidor no
/// procesa imágenes: en shared hosting IIS, redimensionar diez fotos por vehículo es
/// tiempo de CPU que se le saca a todos los tenants a la vez.
/// </para>
/// </remarks>
[ApiController]
[Route("api/vehiculos/{vehiculoId:int}/fotos")]
[Authorize(Policy = Politicas.PanelDeTenant)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public sealed class VehiculoFotosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;
    private readonly ITenantContext _tenantContext;

    public VehiculoFotosController(AppDbContext db, IImageStorage storage, ITenantContext tenantContext)
    {
        _db = db;
        _storage = storage;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VehiculoFotoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehiculoFotoDto>>> Listar(
        int vehiculoId,
        CancellationToken cancellationToken)
    {
        var vehiculo = await BuscarAsync(vehiculoId, cancellationToken).ConfigureAwait(false);

        return vehiculo is null ? NoExiste(vehiculoId) : Ok(Galeria(vehiculo));
    }

    [HttpPost]
    [RequestSizeLimit(ValidacionDeImagen.TamanoMaximoEnBytes + 4096)]
    [ProducesResponseType(typeof(VehiculoFotoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VehiculoFotoDto>> Subir(
        int vehiculoId,
        IFormFile? imagen,
        CancellationToken cancellationToken)
    {
        if (imagen is null)
        {
            return Rechazo("Mandá la imagen en el campo 'imagen'.");
        }

        var vehiculo = await BuscarAsync(vehiculoId, cancellationToken).ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NoExiste(vehiculoId);
        }

        if (vehiculo.Fotos.Count >= ValidacionDeImagen.FotosMaximasPorVehiculo)
        {
            return Rechazo($"Un vehículo admite hasta {ValidacionDeImagen.FotosMaximasPorVehiculo} fotos.");
        }

        await using var contenido = imagen.OpenReadStream();

        var encabezado = new byte[12];
        var leidos = await contenido.ReadAsync(encabezado, cancellationToken).ConfigureAwait(false);

        // El Content-Type lo manda el cliente y no prueba nada: lo que decide es la firma
        // real de los primeros bytes.
        var validacion = ValidacionDeImagen.Validar(encabezado.AsSpan(0, leidos), imagen.Length);

        if (!validacion.EsValida)
        {
            return Rechazo(validacion.Error!);
        }

        contenido.Position = 0;

        var guardada = await _storage.GuardarAsync(
            contenido,
            CarpetaDe(vehiculo),
            validacion.Extension,
            validacion.ContentType,
            cancellationToken).ConfigureAwait(false);

        var foto = new VehiculoFoto
        {
            VehiculoId = vehiculo.Id,
            Url = guardada.Url,
            Orden = SiguienteOrden(vehiculo),

            // La primera foto que entra es la portada. Que un vehículo salga publicado sin
            // imagen porque nadie tildó la portada sería un bug caro y silencioso.
            EsPortada = vehiculo.Fotos.Count == 0,
        };

        _db.VehiculoFotos.Add(foto);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(Listar), new { vehiculoId }, foto.ADto());
    }

    [HttpDelete("{fotoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Borrar(int vehiculoId, int fotoId, CancellationToken cancellationToken)
    {
        var vehiculo = await BuscarAsync(vehiculoId, cancellationToken).ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NoExiste(vehiculoId);
        }

        if (vehiculo.Fotos.FirstOrDefault(f => f.Id == fotoId) is not { } foto)
        {
            return NoExisteFoto(fotoId);
        }

        var eraPortada = foto.EsPortada;
        var clave = VehiculosController.ClaveDe(foto);

        _db.VehiculoFotos.Remove(foto);
        vehiculo.Fotos.Remove(foto);

        // Si se fue la portada, la ocupa la que quedó primera. Un vehículo sin portada se
        // ve sin foto en el listado, y eso lo saca de la consideración del comprador.
        if (eraPortada && vehiculo.Fotos.OrderBy(f => f.Orden).FirstOrDefault() is { } reemplazo)
        {
            reemplazo.EsPortada = true;
        }

        Renumerar(vehiculo);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // El binario se borra recién cuando la fila ya se fue: al revés, un fallo al
        // guardar dejaría la ficha apuntando a una foto que ya no existe.
        if (clave is not null)
        {
            await _storage.BorrarAsync(clave, cancellationToken).ConfigureAwait(false);
        }

        return NoContent();
    }

    /// <summary>
    /// Reordena la galería. El primer id de la lista queda como portada: en una galería,
    /// "primera" y "portada" son lo mismo para quien la ordena.
    /// </summary>
    [HttpPut("orden")]
    [ProducesResponseType(typeof(IReadOnlyList<VehiculoFotoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<VehiculoFotoDto>>> Reordenar(
        int vehiculoId,
        ReordenarFotosRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehiculo = await BuscarAsync(vehiculoId, cancellationToken).ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NoExiste(vehiculoId);
        }

        var propias = vehiculo.Fotos.Select(f => f.Id).ToHashSet();

        // Se exige la lista completa. Un reordenamiento parcial deja el resto en un orden
        // que nadie eligió, y encima permitiría colar el id de una foto de otro vehículo.
        if (propias.Count != request.FotoIds.Count || !request.FotoIds.All(propias.Contains))
        {
            return Rechazo("El orden tiene que incluir exactamente las fotos de este vehículo.");
        }

        for (var posicion = 0; posicion < request.FotoIds.Count; posicion++)
        {
            var foto = vehiculo.Fotos.First(f => f.Id == request.FotoIds[posicion]);
            foto.Orden = posicion;
            foto.EsPortada = posicion == 0;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(Galeria(vehiculo));
    }

    [HttpPost("{fotoId:int}/portada")]
    [ProducesResponseType(typeof(IReadOnlyList<VehiculoFotoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehiculoFotoDto>>> MarcarPortada(
        int vehiculoId,
        int fotoId,
        CancellationToken cancellationToken)
    {
        var vehiculo = await BuscarAsync(vehiculoId, cancellationToken).ConfigureAwait(false);

        if (vehiculo is null)
        {
            return NoExiste(vehiculoId);
        }

        if (vehiculo.Fotos.All(f => f.Id != fotoId))
        {
            return NoExisteFoto(fotoId);
        }

        foreach (var foto in vehiculo.Fotos)
        {
            foto.EsPortada = foto.Id == fotoId;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(Galeria(vehiculo));
    }

    private Task<Vehiculo?> BuscarAsync(int vehiculoId, CancellationToken cancellationToken)
        => _db.Vehiculos
            .Include(v => v.Fotos)
            .FirstOrDefaultAsync(v => v.Id == vehiculoId, cancellationToken);

    private string CarpetaDe(Vehiculo vehiculo)
    {
        // El tenant sale del contexto del request, no del vehículo: son el mismo por el
        // filtro global, y tomarlo de la fuente de verdad evita que un cambio futuro en
        // la consulta convierta esto en una ruta ajena.
        var tenantId = _tenantContext.TenantId
                       ?? throw new InvalidOperationException("No hay tenant resuelto para guardar la foto.");

        return GeneradorDeClaves.CarpetaDeVehiculo(tenantId, vehiculo.Id);
    }

    private static int SiguienteOrden(Vehiculo vehiculo)
        => vehiculo.Fotos.Count == 0 ? 0 : vehiculo.Fotos.Max(f => f.Orden) + 1;

    /// <summary>Deja el orden sin huecos, para que la galería no dependa de los ids.</summary>
    private static void Renumerar(Vehiculo vehiculo)
    {
        var posicion = 0;

        foreach (var foto in vehiculo.Fotos.OrderBy(f => f.Orden).ToList())
        {
            foto.Orden = posicion++;
        }
    }

    private static IReadOnlyList<VehiculoFotoDto> Galeria(Vehiculo vehiculo)
        => vehiculo.Fotos
            .OrderByDescending(f => f.EsPortada)
            .ThenBy(f => f.Orden)
            .Select(f => f.ADto())
            .ToList();

    private ActionResult Rechazo(string detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status400BadRequest);

    private ActionResult NoExiste(int vehiculoId)
        => Problem(detail: $"No existe el vehículo {vehiculoId}.", statusCode: StatusCodes.Status404NotFound);

    private ActionResult NoExisteFoto(int fotoId)
        => Problem(detail: $"No existe la foto {fotoId}.", statusCode: StatusCodes.Status404NotFound);
}
