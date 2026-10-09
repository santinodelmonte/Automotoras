using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Los errores del navegador —una pantalla que no pudo pintarse, un manejador que tiró—,
/// registrados como error de la API para que lleguen a Sentry con todos los demás.
/// </summary>
/// <remarks>
/// Sin autenticación: el comprador del sitio público no tiene cuenta, y un error puede
/// pasar justo con la sesión vencida. No escribe en la base, solo en el log, y con un
/// límite de tasa propio y más bajo que el de los eventos: un error que se repite en un
/// bucle no puede llenarle a nadie la cuota de Sentry.
/// <para>
/// No se registra nada de quién lo mandó: ni IP, ni navegador, ni usuario. Lo que hace
/// falta para arreglar un error es la pila y la pantalla, no la persona.
/// </para>
/// </remarks>
[ApiController]
[Route("api/errores-del-cliente")]
[AllowAnonymous]
[EnableRateLimiting(LimitesDeErroresDelCliente.Politica)]
public sealed class ErroresDelClienteController : ControllerBase
{
    private readonly ILogger<ErroresDelClienteController> _logger;

    public ErroresDelClienteController(ILogger<ErroresDelClienteController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Registrar(ErrorDelClienteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogError(
            "Error en el navegador en {Ruta}: {Mensaje}\n{Pila}\n{Componentes}",
            Recortar(request.Ruta, 300),
            Recortar(request.Mensaje, 500),
            Recortar(request.Pila, 4000),
            Recortar(request.Componentes, 2000));

        return Accepted();
    }

    private static string Recortar(string? valor, int largo)
        => valor is null ? string.Empty : valor.Length <= largo ? valor : valor[..largo];
}

public sealed record ErrorDelClienteRequest(string? Mensaje, string? Pila, string? Componentes, string? Ruta);

public static class LimitesDeErroresDelCliente
{
    public const string Politica = "errores-del-cliente";

    public const int ErroresPorVentana = 10;
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(1);
}
