using System.Security.Cryptography;
using System.Text;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Infrastructure.Planes;
using Microsoft.Extensions.Options;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Trabajos periódicos, disparados por un cron externo.
/// </summary>
/// <remarks>
/// No hay <c>BackgroundService</c> ni <c>IHostedService</c> en ningún lado, y no es una
/// omisión: el deploy es shared hosting Windows/IIS, donde el app pool recicla cuando
/// quiere. Un job crítico adentro del proceso web se corta a mitad de camino sin que nadie
/// se entere. Como endpoint, el cron externo tiene reintentos, registro y una respuesta
/// HTTP que dice si salió bien.
/// <para>
/// Se autentica con el header <c>X-Job-Secret</c> y no con JWT: el cron no es un usuario,
/// no tiene sesión y no debería poder hacer nada más que esto.
/// </para>
/// </remarks>
[ApiController]
[Route("api/jobs")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class JobsController : ControllerBase
{
    private const string HeaderDelSecreto = "X-Job-Secret";

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuracion;

    public JobsController(AppDbContext db, IConfiguration configuracion)
    {
        _db = db;
        _configuracion = configuracion;
    }

    /// <summary>
    /// Registra la cotización del dólar del día. Idempotente: correrlo dos veces actualiza
    /// la fila en vez de duplicarla, que es lo que hace que el cron pueda reintentar.
    /// </summary>
    [HttpPost("cotizaciones")]
    [ProducesResponseType(typeof(CotizacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CotizacionDto>> Cotizaciones(
        RegistrarCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SecretoCorrecto())
        {
            return Unauthorized();
        }

        var cotizacion = await _db.Cotizaciones
            .FirstOrDefaultAsync(c => c.Fecha == request.Fecha, cancellationToken)
            .ConfigureAwait(false);

        if (cotizacion is null)
        {
            cotizacion = new Cotizacion { Fecha = request.Fecha };
            _db.Cotizaciones.Add(cotizacion);
        }

        cotizacion.UsdUyu = request.UsdUyu;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new CotizacionDto(cotizacion.Fecha, cotizacion.UsdUyu));
    }

    /// <summary>
    /// Qué modelos y años conviene cotizar: los que hay publicados hoy en alguna
    /// automotora.
    /// </summary>
    /// <remarks>
    /// Sin esto el script del cron tendría que cotizar el catálogo entero por doce años de
    /// antigüedad —miles de consultas a MercadoLibre— para terminar guardando precios que
    /// no le sirven a nadie. Cotizar lo que está en góndola es una fracción de eso.
    /// <para>
    /// Es una lectura cross-tenant deliberada, y por eso está acá y no en un endpoint de
    /// tenant: lo único que sale son ids del catálogo global y años, sin precios, sin
    /// cantidades y sin nombre de automotora. Un tenant no puede deducir de esta respuesta
    /// qué tiene otro.
    /// </para>
    /// </remarks>
    [HttpGet("modelos-a-cotizar")]
    [ProducesResponseType(typeof(IReadOnlyList<ModeloACotizarDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModeloACotizarDto>>> ModelosACotizar(
        CancellationToken cancellationToken)
    {
        if (!SecretoCorrecto())
        {
            return Unauthorized();
        }

        var publicados = await _db.Vehiculos
            .IgnoreQueryFilters()
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .Select(v => new
            {
                v.ModeloId,
                Modelo = v.Modelo!.Nombre,
                Marca = v.Modelo.Marca!.Nombre,
                v.Anio,
            })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var modelos = publicados
            .GroupBy(v => (v.ModeloId, v.Marca, v.Modelo))
            .Select(g => new ModeloACotizarDto(
                g.Key.ModeloId,
                g.Key.Marca,
                g.Key.Modelo,
                g.Select(v => v.Anio).Distinct().OrderBy(a => a).ToList()))
            .OrderBy(m => m.Marca, StringComparer.Ordinal)
            .ThenBy(m => m.Modelo, StringComparer.Ordinal)
            .ToList();

        return Ok(modelos);
    }

    /// <summary>
    /// Guarda el snapshot de precios de mercado del día. Idempotente por fuente, modelo,
    /// año y fecha: el cron puede reintentar el lote entero sin duplicar nada.
    /// </summary>
    /// <remarks>
    /// Los modelos que no existen en el catálogo se ignoran en silencio en vez de tumbar
    /// el lote. Quien arma el snapshot trabaja contra una copia del catálogo que puede
    /// estar un día atrasada, y perder mil precios buenos porque uno referencia un modelo
    /// que se dio de baja sería cambiar un dato viejo por ninguno.
    /// </remarks>
    [HttpPost("precios-de-mercado")]
    [ProducesResponseType(typeof(ResultadoDePreciosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoDePreciosDto>> PreciosDeMercado(
        RegistrarPreciosDeMercadoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SecretoCorrecto())
        {
            return Unauthorized();
        }

        var pedidos = request.Precios.Select(p => p.ModeloId).Distinct().ToList();

        var conocidos = await _db.Modelos
            .Where(m => pedidos.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var validos = request.Precios.Where(p => conocidos.Contains(p.ModeloId)).ToList();

        if (validos.Count == 0)
        {
            return Ok(new ResultadoDePreciosDto(0, 0));
        }

        // Los existentes de ese día se traen de una y no de a uno: un lote de mil precios
        // haría mil consultas antes de escribir la primera fila.
        var existentes = await _db.PreciosDeMercado
            .Where(p => p.Fecha == request.Fecha
                        && p.Fuente == request.Fuente
                        && pedidos.Contains(p.ModeloId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var porClave = existentes.ToDictionary(p => (p.ModeloId, p.Anio));
        var guardados = 0;
        var actualizados = 0;

        foreach (var precio in validos)
        {
            if (!porClave.TryGetValue((precio.ModeloId, precio.Anio), out var fila))
            {
                fila = new PrecioDeMercado
                {
                    ModeloId = precio.ModeloId,
                    Anio = precio.Anio,
                    Fuente = request.Fuente,
                    Fecha = request.Fecha,
                };

                _db.PreciosDeMercado.Add(fila);
                porClave[(precio.ModeloId, precio.Anio)] = fila;
                guardados++;
            }
            else
            {
                actualizados++;
            }

            fila.Moneda = Enumeraciones.Parsear<Moneda>(precio.Moneda);
            fila.PrecioMediano = precio.PrecioMediano;
            fila.PrecioMinimo = precio.PrecioMinimo;
            fila.PrecioMaximo = precio.PrecioMaximo;
            fila.Publicaciones = precio.Publicaciones;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new ResultadoDePreciosDto(guardados, actualizados));
    }

    /// <summary>
    /// Compara el header contra el secreto configurado, en tiempo constante.
    /// </summary>
    /// <remarks>
    /// Un <c>==</c> de strings corta en el primer carácter distinto, y esa diferencia de
    /// tiempo se puede medir para adivinar el secreto de a un carácter. Es un endpoint
    /// público: alguien lo va a probar.
    /// </remarks>
    /// <summary>
    /// Avisa por correo a los dueños de las automotoras que están por vencer, en gracia o
    /// suspendidas.
    /// </summary>
    /// <remarks>
    /// El job no cambia ningún estado —el estado se calcula a partir de <c>PagaHasta</c>—:
    /// solo avisa. Cada etapa de cada vencimiento se avisa una vez, y queda registrado. Un
    /// aviso que no sale no se registra, así que la próxima corrida lo reintenta.
    /// </remarks>
    [HttpPost("avisos-de-vencimiento")]
    [ProducesResponseType(typeof(ResultadoDeAvisosDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoDeAvisosDto>> AvisosDeVencimiento(
        [FromServices] INotificadorPorCorreo correo,
        [FromServices] TimeProvider reloj,
        [FromServices] IOptions<OpcionesDeCobranza> opciones,
        [FromServices] ILogger<JobsController> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correo);
        ArgumentNullException.ThrowIfNull(opciones);

        if (!SecretoCorrecto())
        {
            return Unauthorized();
        }

        var hoy = PoliticaDePlanEnBase.Hoy(reloj);

        var vigentes = await _db.Suscripciones
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .Include(s => s.Tenant)
            .Where(s => s.Fin == null && s.Tenant!.Activo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int enviados = 0, yaAvisados = 0, fallidos = 0;

        foreach (var suscripcion in vigentes)
        {
            var estado = CicloDeCobro.Evaluar(suscripcion.PagaHasta, hoy, opciones.Value);

            if (estado == EstadoDeCobro.Vigente)
            {
                continue;
            }

            var avisado = await _db.AvisosDeCobro
                .IgnoreQueryFilters()
                .AnyAsync(
                    a => a.SuscripcionId == suscripcion.Id && a.PagaHasta == suscripcion.PagaHasta && a.Estado == estado,
                    cancellationToken)
                .ConfigureAwait(false);

            if (avisado)
            {
                yaAvisados++;
                continue;
            }

            var duenios = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == suscripcion.TenantId && u.Activo && u.Rol == RolUsuario.Owner)
                .Select(u => u.Email)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (duenios.Count == 0)
            {
                logger.LogWarning("La automotora {TenantId} no tiene ningún Owner activo a quien avisarle.", suscripcion.TenantId);
                fallidos++;
                continue;
            }

            var (asunto, cuerpo) = AvisosDeCobro.Redactar(
                estado, suscripcion.Tenant!.Nombre, suscripcion.Plan!.Nombre, suscripcion.PagaHasta, opciones.Value);

            try
            {
                await correo.EnviarAsync(duenios, asunto, cuerpo, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
            {
                // Un aviso que no sale no corta la corrida: los demás pueden salir igual.
                logger.LogWarning(ex, "No se pudo avisar a la automotora {TenantId}.", suscripcion.TenantId);
                fallidos++;
                continue;
            }

            using (var _ = _db.PermitirEscrituraCrossTenant())
            {
                _db.AvisosDeCobro.Add(new AvisoDeCobro
                {
                    TenantId = suscripcion.TenantId,
                    SuscripcionId = suscripcion.Id,
                    Estado = estado,
                    PagaHasta = suscripcion.PagaHasta,
                    Destinatarios = string.Join(", ", duenios),
                });

                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            enviados++;
        }

        return Ok(new ResultadoDeAvisosDto(enviados, yaAvisados, fallidos, correo.Configurado));
    }

    private bool SecretoCorrecto()
    {
        var esperado = _configuracion["Jobs:Secret"];

        // Sin secreto configurado no se ejecuta ningún job. Fallar cerrado: un secreto
        // vacío que matchee un header vacío deja los jobs abiertos a cualquiera.
        if (string.IsNullOrWhiteSpace(esperado))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(HeaderDelSecreto, out var recibido))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(recibido.ToString()),
            Encoding.UTF8.GetBytes(esperado));
    }
}
