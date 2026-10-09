using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Vehiculos;

/// <summary>
/// Corre la importación de stock contra la base: arma el catálogo, consulta el tope del
/// plan y, si se pidió confirmar y no hubo errores, crea los vehículos.
/// </summary>
/// <remarks>
/// La usan el panel del Owner y el SuperAdmin durante la implementación. Quién puede
/// escribir en qué tenant lo decide quien llama: el Owner escribe en el suyo con la
/// política normal, el SuperAdmin pide el escape cross-tenant.
/// </remarks>
public sealed class ImportadorDeStock
{
    private readonly AppDbContext _db;
    private readonly IValidator<GuardarVehiculoRequest> _validador;
    private readonly IPoliticaDePlan _politica;
    private readonly TimeProvider _reloj;

    public ImportadorDeStock(
        AppDbContext db,
        IValidator<GuardarVehiculoRequest> validador,
        IPoliticaDePlan politica,
        TimeProvider reloj)
    {
        _db = db;
        _validador = validador;
        _politica = politica;
        _reloj = reloj;
    }

    /// <param name="confirmar">Sin confirmar, solo valida y no escribe nada.</param>
    /// <param name="cargarCostos">Si se guarda el precio de costo. Solo quien puede verlo.</param>
    public async Task<ResultadoDeImportacionDto> ProcesarAsync(
        int tenantId,
        string contenido,
        bool confirmar,
        bool cargarCostos,
        CancellationToken cancellationToken = default)
    {
        var catalogo = await CatalogoAsync(cancellationToken).ConfigureAwait(false);
        var situacion = await _politica.SituacionAsync(tenantId, cancellationToken).ConfigureAwait(false);

        int? lugaresLibres = situacion.Plan is null
            ? 0
            : situacion.Plan.MaxVehiculos is { } tope ? tope - situacion.VehiculosPublicados : null;

        var (resultado, altas) = ImportacionDeStock.Analizar(contenido, catalogo, _validador, lugaresLibres);

        if (!confirmar || resultado.Errores.Count > 0)
        {
            return resultado;
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;

        _db.Vehiculos.AddRange(altas.Select(a => new Vehiculo
        {
            TenantId = tenantId,
            ModeloId = a.ModeloId,
            VersionId = a.VersionId,
            Anio = a.Anio,
            Kilometraje = a.Kilometraje,
            Combustible = Enumeraciones.Parsear<Combustible>(a.Combustible),
            Transmision = Enumeraciones.Parsear<Transmision>(a.Transmision),
            Color = a.Color,
            Puertas = a.Puertas,
            Motor = a.Motor,
            Precio = a.Precio,
            Moneda = Enumeraciones.Parsear<Moneda>(a.Moneda),
            Descripcion = a.Descripcion,
            Destacado = a.Destacado,
            PrecioCosto = cargarCostos ? a.PrecioCosto : null,
            Estado = EstadoVehiculo.Disponible,
            FechaPublicacion = ahora,
        }));

        // Un solo SaveChanges: o entran todos, o ninguno.
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return resultado with { Importados = altas.Count };
    }

    /// <summary>Marcas y modelos activos, con sus versiones, indexados por nombre normalizado.</summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModeloDelCatalogo>>> CatalogoAsync(
        CancellationToken cancellationToken)
    {
        var modelos = await _db.Modelos
            .AsNoTracking()
            .Where(m => m.Activo && m.Marca!.Activo)
            .Select(m => new
            {
                m.Id,
                Marca = m.Marca!.Nombre,
                m.Nombre,
                Versiones = m.Versiones.Where(v => v.Activo).Select(v => new { v.Id, v.Nombre }).ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return modelos
            .GroupBy(m => ImportacionDeStock.Normalizar(m.Marca))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, ModeloDelCatalogo>)g.ToDictionary(
                    m => ImportacionDeStock.Normalizar(m.Nombre),
                    m => new ModeloDelCatalogo(
                        m.Id,
                        m.Versiones
                            .GroupBy(v => ImportacionDeStock.Normalizar(v.Nombre))
                            .ToDictionary(v => v.Key, v => v.First().Id))));
    }
}
