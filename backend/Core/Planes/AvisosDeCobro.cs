using System.Globalization;

namespace AutomotoraSaaS.Core.Planes;

/// <summary>
/// Envío de correos. El sistema manda pocos y todos importantes, así que no hay cola: se
/// manda en el momento y, si no sale, el job lo vuelve a intentar en la próxima corrida.
/// </summary>
public interface INotificadorPorCorreo
{
    /// <summary>Si hay un servidor de correo configurado.</summary>
    bool Configurado { get; }

    Task EnviarAsync(IReadOnlyList<string> para, string asunto, string cuerpo, CancellationToken cancellationToken = default);
}

/// <summary>Resultado de una corrida del job de avisos.</summary>
/// <param name="Enviados">Avisos que salieron en esta corrida.</param>
/// <param name="YaAvisados">Automotoras que ya tenían el aviso de esta etapa.</param>
/// <param name="Fallidos">Avisos que no salieron; se reintentan en la próxima corrida.</param>
public sealed record ResultadoDeAvisosDto(int Enviados, int YaAvisados, int Fallidos, bool CorreoConfigurado);

/// <summary>Los textos de los avisos de vencimiento.</summary>
public static class AvisosDeCobro
{
    private static readonly CultureInfo Uruguay = CultureInfo.GetCultureInfo("es-UY");

    public static (string Asunto, string Cuerpo) Redactar(
        EstadoDeCobro estado,
        string automotora,
        string plan,
        DateOnly pagaHasta,
        OpcionesDeCobranza opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        var vence = pagaHasta.ToString("dd/MM/yyyy", Uruguay);
        var suspension = pagaHasta.AddDays(opciones.DiasDeGracia + 1).ToString("dd/MM/yyyy", Uruguay);

        return estado switch
        {
            EstadoDeCobro.PorVencer => (
                $"{automotora}: el abono vence el {vence}",
                $"Hola. El abono del plan {plan} de {automotora} está cubierto hasta el {vence}.\n\n" +
                "Para que el sitio siga funcionando sin interrupciones, coordinemos el próximo pago " +
                "antes de esa fecha. Si ya lo hiciste, ignorá este mensaje."),

            EstadoDeCobro.Gracia => (
                $"{automotora}: el abono está vencido",
                $"Hola. El abono del plan {plan} de {automotora} venció el {vence}.\n\n" +
                $"El sitio sigue publicado hasta el {suspension}. Si para esa fecha no está " +
                "regularizado, el sitio público va a mostrar una página de mantenimiento. El panel " +
                "y todos los datos siguen disponibles."),

            EstadoDeCobro.Suspendido => (
                $"{automotora}: el sitio quedó en mantenimiento",
                $"Hola. El abono del plan {plan} de {automotora} venció el {vence} y el sitio público " +
                "quedó en mantenimiento.\n\n" +
                "No se borró nada: el panel sigue disponible y podés exportar tus datos cuando " +
                "quieras. Apenas se registre el pago, el sitio vuelve a estar en línea."),

            _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Una suscripción al día no se avisa."),
        };
    }
}
