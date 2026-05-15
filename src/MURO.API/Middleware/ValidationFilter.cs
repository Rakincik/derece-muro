using FluentValidation;
using MuroValidationException = MURO.Domain.Exceptions.ValidationException;

namespace MURO.API.Middleware;

/// <summary>
/// FluentValidation otomatik doğrulama filtresi.
/// Controller action'larından ÖNCE çalışır ve request body'yi doğrular.
/// Hata varsa 422 ValidationException fırlatır.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator == null) return await next(context);

        var arg = context.Arguments.OfType<T>().FirstOrDefault();
        if (arg == null) return await next(context);

        var result = await validator.ValidateAsync(arg);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new MuroValidationException(errors);
        }

        return await next(context);
    }
}

/// <summary>
/// MVC Action Filter olarak FluentValidation doğrulaması.
/// [FromBody] ile gelen request modeli otomatik doğrular.
/// </summary>
public class FluentValidationActionFilter : Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public FluentValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg == null) continue;

            var argType = arg.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argType);
            var validator = _serviceProvider.GetService(validatorType) as IValidator;

            if (validator == null) continue;

            var validationContext = new ValidationContext<object>(arg);
            var result = await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                var errors = result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                throw new MuroValidationException(errors);
            }
        }

        await next();
    }
}
