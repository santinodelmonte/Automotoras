using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Planes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AutomotoraSaaS.Api.Planes;

/// <summary>
/// Cómo sale un rechazo por plan: 403 con todo lo que hace falta para entenderlo.
/// </summary>
public static class RespuestasDelPlan
{
    /// <summary>El <c>type</c> del ProblemDetails, para que el frontend lo distinga de un 403 de permisos.</summary>
    public const string Tipo = "limite-del-plan";

    public static ObjectResult Rechazo(this ControllerBase controller, RechazoDelPlan rechazo)
    {
        ArgumentNullException.ThrowIfNull(controller);
        return Construir(rechazo);
    }

    public static ObjectResult Construir(RechazoDelPlan rechazo)
    {
        ArgumentNullException.ThrowIfNull(rechazo);

        var problema = new ProblemDetails
        {
            Type = Tipo,
            Title = "Límite del plan",
            Detail = rechazo.Detalle,
            Status = StatusCodes.Status403Forbidden,
        };

        problema.Extensions["recurso"] = rechazo.Recurso;
        problema.Extensions["plan"] = rechazo.Plan;
        problema.Extensions["tope"] = rechazo.Tope;
        problema.Extensions["uso"] = rechazo.Uso;

        return new ObjectResult(problema) { StatusCode = StatusCodes.Status403Forbidden };
    }
}

/// <summary>
/// La acción solo corre si el plan vigente del tenant incluye la funcionalidad.
/// </summary>
/// <remarks>
/// Es un filtro y no un middleware: lo que se protege es un controller entero (reportes,
/// benchmark), y el filtro corre ya con el tenant resuelto y la autorización hecha.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiereDelPlanAttribute : TypeFilterAttribute
{
    public RequiereDelPlanAttribute(FuncionDelPlan funcion)
        : base(typeof(FiltroDeFuncionDelPlan))
    {
        Arguments = [funcion];
    }

    private sealed class FiltroDeFuncionDelPlan : IAsyncActionFilter
    {
        private readonly FuncionDelPlan _funcion;
        private readonly IPoliticaDePlan _politica;
        private readonly ITenantContext _tenant;

        public FiltroDeFuncionDelPlan(FuncionDelPlan funcion, IPoliticaDePlan politica, ITenantContext tenant)
        {
            _funcion = funcion;
            _politica = politica;
            _tenant = tenant;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            // Sin tenant no hay a quién consultarle el plan. Los endpoints que usan este
            // filtro son todos de tenant, así que llegar acá sin uno es fallar cerrado.
            if (_tenant.TenantId is not { } tenantId)
            {
                context.Result = new ForbidResult();
                return;
            }

            var situacion = await _politica
                .SituacionAsync(tenantId, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (situacion.Usar(_funcion) is { } rechazo)
            {
                context.Result = RespuestasDelPlan.Construir(rechazo);
                return;
            }

            await next().ConfigureAwait(false);
        }
    }
}
