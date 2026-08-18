namespace AutomotoraSaaS.Infrastructure.Dominios;

/// <summary>
/// Configuración del alta de dominios propios. Sección <c>Dominios</c> de <c>appsettings</c>.
/// </summary>
/// <remarks>
/// El destino del tráfico es configuración y no una constante porque cambia con el hosting,
/// y el día que cambie hay que poder moverlo sin recompilar: las instrucciones de DNS que
/// se le dan a cada automotora salen de acá.
/// </remarks>
public sealed class OpcionesDeDominios
{
    public const string Seccion = "Dominios";

    /// <summary>
    /// Adónde apunta el CNAME de un subdominio. Vacío significa "todavía no lo sabemos", y
    /// entonces no se dictan instrucciones: mandar a alguien a apuntar su DNS a una IP
    /// inventada es peor que no decirle nada.
    /// </summary>
    public string? DestinoCname { get; set; }

    /// <summary>IP para el registro A del apex, que no admite CNAME.</summary>
    public string? DestinoIp { get; set; }

    /// <summary>
    /// Cuántas reverificaciones seguidas tienen que fallar antes de apagar un dominio que
    /// ya estaba andando. Un DNS lento o una zona en propagación no pueden bajarle el sitio
    /// a una automotora.
    /// </summary>
    public int ToleranciaDeFallos { get; set; } = 3;

    /// <summary>Cada cuánto se revisa un dominio que ya verificó.</summary>
    public int DiasEntreReverificaciones { get; set; } = 7;

    /// <summary>
    /// Tope de dominios por corrida del cron. El job es un request HTTP y tiene que
    /// terminar; si quedan más, los toma la corrida siguiente.
    /// </summary>
    public int MaximoPorCorrida { get; set; } = 200;
}
