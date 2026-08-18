using AutomotoraSaaS.Api.Auth;
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
/// Catálogo de marcas, modelos y versiones, y las solicitudes de alta de modelos.
/// </summary>
/// <remarks>
/// El catálogo es global y de solo lectura para las automotoras: lo administra el
/// SuperAdmin. Esa es la regla que sostiene toda la analítica —si el vendedor pudiera
/// escribir "VW", "Volkswagen" y "volkswagen ", cualquier agregación posterior sería
/// basura irrecuperable—. Cuando falta un modelo, el vendedor lo <em>solicita</em>; no lo
/// crea.
/// </remarks>
[ApiController]
[Route("api/catalogo")]
[Authorize(Policy = Politicas.PanelDeTenant)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CatalogoController : ControllerBase
{
    private readonly AppDbContext _db;

    public CatalogoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("marcas")]
    [ProducesResponseType(typeof(IReadOnlyList<MarcaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MarcaDto>>> Marcas(CancellationToken cancellationToken)
    {
        var marcas = await _db.Marcas
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new MarcaDto(m.Id, m.Nombre, m.Activo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(marcas);
    }

    [HttpGet("marcas/{marcaId:int}/modelos")]
    [ProducesResponseType(typeof(IReadOnlyList<ModeloDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModeloDto>>> Modelos(
        int marcaId,
        CancellationToken cancellationToken)
    {
        var modelos = await _db.Modelos
            .Where(m => m.MarcaId == marcaId && m.Activo)
            .OrderBy(m => m.Nombre)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(modelos
            .Select(m => new ModeloDto(m.Id, m.MarcaId, m.Nombre, m.Carroceria.ToString(), m.Activo))
            .ToList());
    }

    [HttpGet("modelos/{modeloId:int}/versiones")]
    [ProducesResponseType(typeof(IReadOnlyList<VersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VersionDto>>> Versiones(
        int modeloId,
        CancellationToken cancellationToken)
    {
        var versiones = await _db.Versiones
            .Where(v => v.ModeloId == modeloId && v.Activo)
            .OrderBy(v => v.Nombre)
            .Select(v => new VersionDto(v.Id, v.ModeloId, v.Nombre, v.Activo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(versiones);
    }

    /// <summary>
    /// Las opciones fijas del formulario. Duplicar los enums en el frontend garantiza que
    /// algún día el select ofrezca un valor que la API rechaza.
    /// </summary>
    [HttpGet("opciones")]
    [ProducesResponseType(typeof(OpcionesDeCatalogoDto), StatusCodes.Status200OK)]
    public ActionResult<OpcionesDeCatalogoDto> Opciones()
        => Ok(new OpcionesDeCatalogoDto(
            Enumeraciones.Nombres<Carroceria>(),
            Enumeraciones.Nombres<Combustible>(),
            Enumeraciones.Nombres<Transmision>(),
            Enumeraciones.Nombres<Moneda>(),
            Enumeraciones.Nombres<EstadoVehiculo>()));

    [HttpGet("solicitudes-modelo")]
    [ProducesResponseType(typeof(IReadOnlyList<SolicitudModeloDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SolicitudModeloDto>>> Solicitudes(
        CancellationToken cancellationToken)
    {
        var solicitudes = await ConVinculos()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(solicitudes.Select(MapeosDeSolicitud.ADto).ToList());
    }

    [HttpPost("solicitudes-modelo")]
    [ProducesResponseType(typeof(SolicitudModeloDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudModeloDto>> Solicitar(
        CrearSolicitudModeloRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (User.IdDeUsuario() is not { } usuarioId)
        {
            return Unauthorized();
        }

        var marcaValida = await _db.Marcas
            .AnyAsync(m => m.Id == request.MarcaId && m.Activo, cancellationToken)
            .ConfigureAwait(false);

        if (!marcaValida)
        {
            return Problem(
                detail: "La marca no existe o está dada de baja.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var nombre = request.NombreModelo.Trim();

        // Si el modelo ya está, no hay nada que solicitar: lo que falta es que el vendedor
        // lo encuentre en el select, y mandarlo a esperar una aprobación sería peor.
        var yaExiste = await _db.Modelos
            .AnyAsync(m => m.MarcaId == request.MarcaId && m.Nombre == nombre, cancellationToken)
            .ConfigureAwait(false);

        if (yaExiste)
        {
            return Problem(
                detail: "Ese modelo ya está en el catálogo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var duplicada = await _db.SolicitudesModelo
            .AnyAsync(
                s => s.MarcaId == request.MarcaId
                     && s.NombreModelo == nombre
                     && s.Estado == EstadoSolicitudModelo.Pendiente,
                cancellationToken)
            .ConfigureAwait(false);

        if (duplicada)
        {
            return Problem(
                detail: "Ya hay una solicitud pendiente para ese modelo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var solicitud = new SolicitudModelo
        {
            // El tenant lo sella SaveChanges con el del token.
            SolicitadaPorUserId = usuarioId,
            MarcaId = request.MarcaId,
            NombreModelo = nombre,
            Carroceria = Enumeraciones.Parsear<Carroceria>(request.Carroceria),
        };

        _db.SolicitudesModelo.Add(solicitud);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var creada = await ConVinculos()
            .FirstAsync(s => s.Id == solicitud.Id, cancellationToken)
            .ConfigureAwait(false);

        return CreatedAtAction(nameof(Solicitudes), null, creada.ADto());
    }

    private IQueryable<SolicitudModelo> ConVinculos()
        => _db.SolicitudesModelo
            .Include(s => s.Marca)
            .Include(s => s.SolicitadaPor);
}
