using System.Net;
using System.Net.Mail;
using AutomotoraSaaS.Core.Planes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Infrastructure.Correo;

/// <summary>Servidor de correo saliente (<c>Correo:*</c>).</summary>
public sealed class OpcionesDeCorreo
{
    public const string Seccion = "Correo";

    /// <summary>Vacío es "sin correo configurado": los avisos no salen y se reintentan.</summary>
    public string? Host { get; set; }

    public int Puerto { get; set; } = 587;
    public bool UsarSsl { get; set; } = true;
    public string? Usuario { get; set; }
    public string? Password { get; set; }

    /// <summary>Dirección del remitente, por ejemplo <c>avisos@auramarketing.uy</c>.</summary>
    public string? Remitente { get; set; }

    public bool EstaCompleto => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Remitente);
}

/// <summary>
/// Envío por SMTP con lo que trae el framework.
/// </summary>
/// <remarks>
/// <c>SmtpClient</c> alcanza para los pocos correos que manda el sistema, y no suma una
/// dependencia. Si algún día hace falta un proveedor con API, se cambia esta clase y nada
/// más.
/// <para>
/// Sin servidor configurado no inventa un envío exitoso: lanza, y el job no deja registro,
/// así que el aviso sale en la primera corrida después de configurarlo.
/// </para>
/// </remarks>
public sealed class NotificadorSmtp : INotificadorPorCorreo
{
    private readonly OpcionesDeCorreo _opciones;
    private readonly ILogger<NotificadorSmtp> _logger;

    public NotificadorSmtp(IOptions<OpcionesDeCorreo> opciones, ILogger<NotificadorSmtp> logger)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _logger = logger;
    }

    public bool Configurado => _opciones.EstaCompleto;

    public async Task EnviarAsync(
        IReadOnlyList<string> para,
        string asunto,
        string cuerpo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(para);

        if (!Configurado)
        {
            throw new InvalidOperationException(
                "No hay servidor de correo configurado (Correo:Host y Correo:Remitente).");
        }

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_opciones.Remitente!),
            Subject = asunto,
            Body = cuerpo,
            IsBodyHtml = false,
        };

        foreach (var direccion in para)
        {
            mensaje.To.Add(direccion);
        }

        using var cliente = new SmtpClient(_opciones.Host, _opciones.Puerto)
        {
            EnableSsl = _opciones.UsarSsl,
            Credentials = string.IsNullOrWhiteSpace(_opciones.Usuario)
                ? null
                : new NetworkCredential(_opciones.Usuario, _opciones.Password),
        };

        await cliente.SendMailAsync(mensaje, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Correo enviado a {Destinatarios}: {Asunto}", string.Join(", ", para), asunto);
    }
}
