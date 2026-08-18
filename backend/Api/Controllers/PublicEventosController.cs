using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Infrastructure.Analitica;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Registro de eventos del sitio público. Es la tabla que alimenta todos los reportes de
/// demanda.
/// </summary>
/// <remarks>
/// Se instrumenta desde el primer día, antes de que exista un solo reporte: los datos de
/// demanda solo valen acumulados en el tiempo, y lo que no se mide hoy no se recupera
/// nunca.
/// <para>
/// Sin autenticación, porque quien navega el sitio no tiene cuenta. Por eso tiene tres
/// candados: límite de tasa por IP, el tenant resuelto por el servidor —nunca por el
/// cuerpo del request— y la verificación de que el vehículo es realmente de ese tenant.
/// Sin el último, cualquiera podría inflarle las visitas a la unidad de otra automotora.
/// </para>
/// </remarks>
[ApiController]
[Route("api/public/events")]
[AllowAnonymous]
[EnableRateLimiting(LimitesDeEventos.Politica)]
public sealed class PublicEventosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly HasheadorDeIp _hasheador;

    public PublicEventosController(AppDbContext db, ITenantContext tenantContext, HasheadorDeIp hasheador)
    {
        _db = db;
        _tenantContext = tenantContext;
        _hasheador = hasheador;
    }

    /// <summary>
    /// Registra un evento. Responde 202: el cliente no espera nada de vuelta y no tiene
    /// sentido que la navegación dependa de que la métrica se haya guardado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Registrar(
        RegistrarEventoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenantContext.TenantId is not { } tenantId)
        {
            return NotFound();
        }

        var tipo = Enumeraciones.Parsear<TipoEvento>(request.Tipo);

        if (request.VehiculoId is { } vehiculoId)
        {
            // El filtro global ya recorta al tenant del request: si el vehículo es de otra
            // automotora, sencillamente no existe para esta consulta.
            var esDeEsteTenant = await _db.Vehiculos
                .AnyAsync(v => v.Id == vehiculoId, cancellationToken)
                .ConfigureAwait(false);

            if (!esDeEsteTenant)
            {
                return NotFound();
            }
        }

        _db.Eventos.Add(new Evento
        {
            VehiculoId = request.VehiculoId,
            Tipo = tipo,
            SessionId = Recortar(request.SessionId, 64),
            IpHash = _hasheador.Hashear(HttpContext.Connection.RemoteIpAddress, tenantId),
            UserAgent = Recortar(Request.Headers.UserAgent.ToString(), 400),
            Referer = Recortar(Request.Headers.Referer.ToString(), 500),
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Accepted();
    }

    private static string? Recortar(string? valor, int largo)
        => string.IsNullOrWhiteSpace(valor) ? null : valor[..Math.Min(valor.Length, largo)];
}

/// <summary>
/// Límite de tasa del endpoint de eventos.
/// </summary>
/// <remarks>
/// Es un endpoint sin autenticación que escribe en la tabla que más crece del sistema. Sin
/// tope, un script deja la base llena de eventos falsos y, de paso, los reportes de todas
/// las automotoras sin valor. El límite es por IP y generoso: una visita normal genera
/// unos pocos eventos por minuto, no cien.
/// </remarks>
public static class LimitesDeEventos
{
    public const string Politica = "eventos-publicos";

    public const int EventosPorVentana = 60;
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(1);
}
