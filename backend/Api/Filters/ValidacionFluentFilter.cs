using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AutomotoraSaaS.Api.Filters;

/// <summary>
/// Corre el validador de FluentValidation que corresponda a cada argumento de la acción,
/// antes de que la acción se ejecute.
/// </summary>
/// <remarks>
/// Se hace con un filtro propio y no con la integración automática de FluentValidation
/// para MVC, que está deprecada desde la versión 11. Son treinta líneas y evita quedar
/// atado a un paquete que su autor pide no usar.
/// <para>
/// Un request inválido sale como <c>ValidationProblemDetails</c> con 400, que es la misma
/// forma que ya devuelve <c>[ApiController]</c> para los errores de binding. Un solo
/// formato de error para el cliente.
/// </para>
/// </remarks>
public sealed class ValidacionFluentFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _servicios;

    public ValidacionFluentFilter(IServiceProvider servicios)
    {
        _servicios = servicios;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var errores = new ModelStateDictionary();

        foreach (var (nombre, argumento) in context.ActionArguments)
        {
            if (argumento is null)
            {
                continue;
            }

            var tipoDelValidador = typeof(IValidator<>).MakeGenericType(argumento.GetType());

            if (_servicios.GetService(tipoDelValidador) is not IValidator validador)
            {
                continue;
            }

            var contexto = new ValidationContext<object>(argumento);
            var resultado = await validador.ValidateAsync(contexto, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            foreach (var error in resultado.Errors)
            {
                // El prefijo con el nombre del argumento es lo que hace que el cliente
                // pueda mapear el error al campo cuando la acción recibe más de un objeto.
                var campo = string.IsNullOrEmpty(error.PropertyName)
                    ? nombre
                    : error.PropertyName;

                errores.AddModelError(campo, error.ErrorMessage);
            }
        }

        if (!errores.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errores));
            return;
        }

        await next().ConfigureAwait(false);
    }
}
