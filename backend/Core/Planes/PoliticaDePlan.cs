using AutomotoraSaaS.Core.Entities;

namespace AutomotoraSaaS.Core.Planes;

/// <summary>
/// Qué plan tiene una automotora, cuánto está usando de él y en qué estado de cobro está.
/// </summary>
/// <remarks>
/// Se consulta desde los controllers y no desde un middleware: el middleware no sabe si la
/// operación en curso publica un vehículo o solo lo edita, y esa distinción es justamente
/// el límite.
/// </remarks>
public interface IPoliticaDePlan
{
    Task<SituacionDelPlan> SituacionAsync(int tenantId, CancellationToken cancellationToken = default);
}

/// <summary>Una funcionalidad que depende del plan.</summary>
public enum FuncionDelPlan
{
    Reportes = 1,
    Benchmark = 2,
    DominioPropio = 3,
}

/// <summary>
/// Foto del plan de una automotora en este momento.
/// </summary>
/// <param name="Plan">Nulo si no tiene suscripción vigente.</param>
/// <param name="VehiculosPublicados">Disponibles más reservados: lo que ocupa lugar en la vidriera.</param>
/// <param name="UsuariosActivos">Usuarios que pueden entrar al panel.</param>
public sealed record SituacionDelPlan(
    Plan? Plan,
    Suscripcion? Suscripcion,
    EstadoDeCobro Estado,
    int? DiasParaVencer,
    int VehiculosPublicados,
    int UsuariosActivos)
{
    /// <summary>
    /// A partir de qué fracción del tope el panel avisa. Que el cliente se entere del
    /// techo cuando lo choca es una mala forma de venderle un upgrade.
    /// </summary>
    public const double UmbralDeAviso = 0.8;

    public bool Incluye(FuncionDelPlan funcion) => Plan is not null && funcion switch
    {
        FuncionDelPlan.Reportes => Plan.IncluyeReportes,
        FuncionDelPlan.Benchmark => Plan.IncluyeBenchmark,
        FuncionDelPlan.DominioPropio => Plan.IncluyeDominioPropio,
        _ => false,
    };

    /// <summary>
    /// Si se puede publicar una unidad más. Por encima del tope —por ejemplo después de
    /// bajar de plan— no se borra nada: se bloquea publicar más hasta volver debajo.
    /// </summary>
    public RechazoDelPlan? PublicarOtroVehiculo()
        => Plan is null
            ? SinPlan("vehiculos")
            : Plan.MaxVehiculos is { } tope && VehiculosPublicados >= tope
                ? new RechazoDelPlan(
                    "vehiculos",
                    $"El plan {Plan.Nombre} permite hasta {tope} vehículos publicados y ya hay " +
                    $"{VehiculosPublicados}. Para publicar más, pausá o marcá como vendida alguna " +
                    "unidad, o pasá a un plan más grande.",
                    Plan.Nombre,
                    tope,
                    VehiculosPublicados)
                : null;

    public RechazoDelPlan? ActivarOtroUsuario()
        => Plan is null
            ? SinPlan("usuarios")
            : Plan.MaxUsuarios is { } tope && UsuariosActivos >= tope
                ? new RechazoDelPlan(
                    "usuarios",
                    $"El plan {Plan.Nombre} permite hasta {tope} usuarios y ya hay {UsuariosActivos} " +
                    "activos. Para sumar otro, dá de baja alguno o pasá a un plan más grande.",
                    Plan.Nombre,
                    tope,
                    UsuariosActivos)
                : null;

    public RechazoDelPlan? Usar(FuncionDelPlan funcion)
        => Plan is null
            ? SinPlan(Recurso(funcion))
            : Incluye(funcion)
                ? null
                : new RechazoDelPlan(
                    Recurso(funcion),
                    $"{Descripcion(funcion)} no está incluido en el plan {Plan.Nombre}. Se habilita " +
                    "cambiando a un plan que lo incluya.",
                    Plan.Nombre,
                    Tope: null,
                    Uso: null);

    private static RechazoDelPlan SinPlan(string recurso)
        => new(
            recurso,
            "La automotora no tiene un plan vigente. Comunicate con nosotros para regularizarlo.",
            Plan: null,
            Tope: null,
            Uso: null);

    private static string Recurso(FuncionDelPlan funcion) => funcion switch
    {
        FuncionDelPlan.Reportes => "reportes",
        FuncionDelPlan.Benchmark => "benchmark",
        FuncionDelPlan.DominioPropio => "dominio-propio",
        _ => funcion.ToString(),
    };

    private static string Descripcion(FuncionDelPlan funcion) => funcion switch
    {
        FuncionDelPlan.Reportes => "El reporte de demanda",
        FuncionDelPlan.Benchmark => "La comparación contra el mercado",
        FuncionDelPlan.DominioPropio => "El dominio propio",
        _ => funcion.ToString(),
    };
}

/// <summary>
/// Por qué el plan no deja hacer algo. Viaja entero en la respuesta: un 403 seco manda al
/// cliente a llamar por teléfono enojado; este dice cuál es el tope, cuánto está usando y
/// cómo se resuelve.
/// </summary>
public sealed record RechazoDelPlan(string Recurso, string Detalle, string? Plan, int? Tope, int? Uso);
