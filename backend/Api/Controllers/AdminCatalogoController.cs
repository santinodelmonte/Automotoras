using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Catalogo;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Administración del catálogo global: marcas, modelos y versiones.
/// </summary>
/// <remarks>
/// El catálogo lo mantiene el SuperAdmin y nadie más. Es la regla que sostiene toda la
/// analítica de demanda: si cada automotora pudiera cargar sus marcas, en un año habría
/// "VW", "Volkswagen" y "volkswagen ", y cruzar datos entre tenants —que es el producto—
/// sería imposible.
/// <para>
/// Nada se borra: se da de baja. Un modelo puede estar referenciado por vehículos ya
/// publicados, y borrarlo dejaría fichas rotas y reportes con agujeros.
/// </para>
/// </remarks>
[ApiController]
[Route("api/admin/catalogo")]
[Authorize(Policy = Politicas.SoloSuperAdmin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AdminCatalogoController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminCatalogoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("marcas")]
    [ProducesResponseType(typeof(IReadOnlyList<MarcaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MarcaDto>>> Marcas(CancellationToken cancellationToken)
    {
        // A diferencia del catálogo del panel, acá salen también las dadas de baja: son
        // justamente las que hay que poder volver a habilitar.
        var marcas = await _db.Marcas
            .OrderBy(m => m.Nombre)
            .Select(m => new MarcaDto(m.Id, m.Nombre, m.Activo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(marcas);
    }

    [HttpPost("marcas")]
    [ProducesResponseType(typeof(MarcaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MarcaDto>> CrearMarca(
        GuardarMarcaRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nombre = request.Nombre.Trim();

        if (await _db.Marcas.AnyAsync(m => m.Nombre == nombre, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto("Ya hay una marca con ese nombre.");
        }

        var marca = new Marca { Nombre = nombre, Activo = request.Activo };

        _db.Marcas.Add(marca);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(Marcas), null, new MarcaDto(marca.Id, marca.Nombre, marca.Activo));
    }

    [HttpPut("marcas/{id:int}")]
    [ProducesResponseType(typeof(MarcaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarcaDto>> ActualizarMarca(
        int id,
        GuardarMarcaRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var marca = await _db.Marcas.FirstOrDefaultAsync(m => m.Id == id, cancellationToken).ConfigureAwait(false);

        if (marca is null)
        {
            return NoExiste("la marca", id);
        }

        var nombre = request.Nombre.Trim();

        if (await _db.Marcas.AnyAsync(m => m.Nombre == nombre && m.Id != id, cancellationToken).ConfigureAwait(false))
        {
            return Conflicto("Ya hay otra marca con ese nombre.");
        }

        marca.Nombre = nombre;
        marca.Activo = request.Activo;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new MarcaDto(marca.Id, marca.Nombre, marca.Activo));
    }

    [HttpGet("modelos")]
    [ProducesResponseType(typeof(IReadOnlyList<ModeloDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModeloDto>>> Modelos(
        [FromQuery] int? marcaId,
        CancellationToken cancellationToken)
    {
        var consulta = _db.Modelos.AsQueryable();

        if (marcaId is { } filtro)
        {
            consulta = consulta.Where(m => m.MarcaId == filtro);
        }

        var modelos = await consulta
            .OrderBy(m => m.Nombre)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(modelos
            .Select(m => new ModeloDto(m.Id, m.MarcaId, m.Nombre, m.Carroceria.ToString(), m.Activo))
            .ToList());
    }

    [HttpPost("modelos")]
    [ProducesResponseType(typeof(ModeloDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeloDto>> CrearModelo(
        GuardarModeloRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await _db.Marcas.AnyAsync(m => m.Id == request.MarcaId, cancellationToken).ConfigureAwait(false))
        {
            return Problem(detail: "La marca no existe.", statusCode: StatusCodes.Status400BadRequest);
        }

        var nombre = request.Nombre.Trim();

        var duplicado = await _db.Modelos
            .AnyAsync(m => m.MarcaId == request.MarcaId && m.Nombre == nombre, cancellationToken)
            .ConfigureAwait(false);

        if (duplicado)
        {
            return Conflicto("Esa marca ya tiene un modelo con ese nombre.");
        }

        var modelo = new Modelo
        {
            MarcaId = request.MarcaId,
            Nombre = nombre,
            Carroceria = Enumeraciones.Parsear<Carroceria>(request.Carroceria),
            Activo = request.Activo,
        };

        _db.Modelos.Add(modelo);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(Modelos),
            null,
            new ModeloDto(modelo.Id, modelo.MarcaId, modelo.Nombre, modelo.Carroceria.ToString(), modelo.Activo));
    }

    [HttpPut("modelos/{id:int}")]
    [ProducesResponseType(typeof(ModeloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModeloDto>> ActualizarModelo(
        int id,
        GuardarModeloRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var modelo = await _db.Modelos.FirstOrDefaultAsync(m => m.Id == id, cancellationToken).ConfigureAwait(false);

        if (modelo is null)
        {
            return NoExiste("el modelo", id);
        }

        var nombre = request.Nombre.Trim();

        var duplicado = await _db.Modelos
            .AnyAsync(m => m.MarcaId == request.MarcaId && m.Nombre == nombre && m.Id != id, cancellationToken)
            .ConfigureAwait(false);

        if (duplicado)
        {
            return Conflicto("Esa marca ya tiene otro modelo con ese nombre.");
        }

        modelo.MarcaId = request.MarcaId;
        modelo.Nombre = nombre;
        modelo.Carroceria = Enumeraciones.Parsear<Carroceria>(request.Carroceria);
        modelo.Activo = request.Activo;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new ModeloDto(modelo.Id, modelo.MarcaId, modelo.Nombre, modelo.Carroceria.ToString(), modelo.Activo));
    }

    [HttpGet("versiones")]
    [ProducesResponseType(typeof(IReadOnlyList<VersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VersionDto>>> Versiones(
        [FromQuery] int? modeloId,
        CancellationToken cancellationToken)
    {
        var consulta = _db.Versiones.AsQueryable();

        if (modeloId is { } filtro)
        {
            consulta = consulta.Where(v => v.ModeloId == filtro);
        }

        var versiones = await consulta
            .OrderBy(v => v.Nombre)
            .Select(v => new VersionDto(v.Id, v.ModeloId, v.Nombre, v.Activo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(versiones);
    }

    [HttpPost("versiones")]
    [ProducesResponseType(typeof(VersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VersionDto>> CrearVersion(
        GuardarVersionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await _db.Modelos.AnyAsync(m => m.Id == request.ModeloId, cancellationToken).ConfigureAwait(false))
        {
            return Problem(detail: "El modelo no existe.", statusCode: StatusCodes.Status400BadRequest);
        }

        var nombre = request.Nombre.Trim();

        var duplicada = await _db.Versiones
            .AnyAsync(v => v.ModeloId == request.ModeloId && v.Nombre == nombre, cancellationToken)
            .ConfigureAwait(false);

        if (duplicada)
        {
            return Conflicto("Ese modelo ya tiene una versión con ese nombre.");
        }

        var version = new VersionVehiculo
        {
            ModeloId = request.ModeloId,
            Nombre = nombre,
            Activo = request.Activo,
        };

        _db.Versiones.Add(version);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(Versiones),
            null,
            new VersionDto(version.Id, version.ModeloId, version.Nombre, version.Activo));
    }

    [HttpPut("versiones/{id:int}")]
    [ProducesResponseType(typeof(VersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VersionDto>> ActualizarVersion(
        int id,
        GuardarVersionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var version = await _db.Versiones
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (version is null)
        {
            return NoExiste("la versión", id);
        }

        version.ModeloId = request.ModeloId;
        version.Nombre = request.Nombre.Trim();
        version.Activo = request.Activo;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new VersionDto(version.Id, version.ModeloId, version.Nombre, version.Activo));
    }

    private ActionResult Conflicto(string detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status409Conflict);

    private ActionResult NoExiste(string que, int id)
        => Problem(detail: $"No existe {que} {id}.", statusCode: StatusCodes.Status404NotFound);
}
