using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;

namespace MURO.API.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireFeatureAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _featureName;

    public RequireFeatureAttribute(string featureName)
    {
        _featureName = featureName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Tenant bilgisi HTTP Context'ten alınır (Tenant middleware'den gelmeli)
        var tenantContext = context.HttpContext.RequestServices.GetService<ITenantService>();
        if (tenantContext?.CurrentTenant == null)
        {
            // Eğer multi-tenant yapı yoksa veya tenant bulunamadıysa erişim engellenebilir
            context.Result = new ForbidResult();
            return;
        }

        var tenant = tenantContext.CurrentTenant;
        if (tenant.Features == null || tenant.Features.Count == 0)
        {
            context.Result = new ObjectResult(new { message = $"This feature '{_featureName}' is not enabled for your plan." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (!tenant.Features.TryGetValue(_featureName, out bool isEnabled) || !isEnabled)
        {
            context.Result = new ObjectResult(new { message = $"This feature '{_featureName}' is not enabled for your plan." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Özellik açık, devam et
        await next();
    }
}
