using AutomotoraSaaS.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AutomotoraSaaS.Api.Filters;

/// <summary>
/// Con una contraseña provisoria no se puede hacer nada más que cambiarla.
/// </summary>
/// <remarks>
/// Se aplica en el servidor y no solo en la pantalla: si dependiera del frontend, la
/// contraseña que puso otra persona seguiría sirviendo para todo con solo llamar a la API.
/// Los endpoints de sesión llevan <see cref="PermitidoConPasswordProvisoriaAttribute"/>, y
/// el sitio público queda afuera porque no depende de quién mira.
/// </remarks>
public sealed class PasswordProvisoriaFilter : IAsyncActionFilter
{
    /// <summary>El <c>type</c> del ProblemDetails, para que el frontend mande a cambiarla.</summary>
    public const string Tipo = "debe-cambiar-password";

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var permitido = context.ActionDescriptor.EndpointMetadata.OfType<PermitidoConPasswordProvisoriaAttribute>().Any()
                        || context.HttpContext.Request.Path.StartsWithSegments("/api/public", StringComparison.OrdinalIgnoreCase);

        if (permitido || !context.HttpContext.User.TienePasswordProvisoria())
        {
            return next();
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Type = Tipo,
            Title = "Cambiá tu contraseña",
            Detail = "Tu contraseña es provisoria. Cambiala para seguir usando el panel.",
            Status = StatusCodes.Status403Forbidden,
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };

        return Task.CompletedTask;
    }
}

/// <summary>La acción se puede usar con una contraseña provisoria: login, sesión y el cambio en sí.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PermitidoConPasswordProvisoriaAttribute : Attribute;
