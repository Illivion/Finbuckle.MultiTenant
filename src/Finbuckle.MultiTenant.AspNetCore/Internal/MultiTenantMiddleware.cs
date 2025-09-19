// Copyright Finbuckle LLC, Andrew White, and Contributors.
// Refer to the solution LICENSE file for more information.

using System.Threading.Tasks;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Options;
using Finbuckle.MultiTenant.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Finbuckle.MultiTenant.AspNetCore.Internal;

/// <summary>
/// Middleware for resolving the MultiTenantContext and storing it in HttpContext.
/// </summary>
public class MultiTenantMiddleware
{
    private readonly RequestDelegate next;
    private readonly ShortCircuitWhenOptions? options;
    private readonly FilteringOptions? filteringOptions;

    public MultiTenantMiddleware(RequestDelegate next)
    {
            this.next = next;
        }

    public MultiTenantMiddleware(RequestDelegate next, IOptions<ShortCircuitWhenOptions> options, IOptions<FilteringOptions> filteringOptions)
    {
            this.next = next;
            this.options = options.Value;
            this.filteringOptions = filteringOptions.Value;
        }

    public async Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        
        var endpointMetadata = endpoint?.Metadata;

        if (filteringOptions?.FilterDelegate != null && endpointMetadata != null)
        {
            var filterRequest = filteringOptions.FilterDelegate(context);

            if (filterRequest)
            {
                await next(context);
                return;
            }
        }

        if (endpointMetadata?.GetMetadata<IExcludeFromMultiTenantResolutionMetadata>() is
            { ExcludeFromResolution: true })
        {
            await next(context);
            return;
        }

        context.RequestServices.GetRequiredService<IMultiTenantContextAccessor>();
        var mtcSetter = context.RequestServices.GetRequiredService<IMultiTenantContextSetter>();

        var resolver = context.RequestServices.GetRequiredService<ITenantResolver>();

        var multiTenantContext = await resolver.ResolveAsync(context).ConfigureAwait(false);
        mtcSetter.MultiTenantContext = multiTenantContext;
        context.Items[typeof(IMultiTenantContext)] = multiTenantContext;

        if (options?.Predicate is null || !options.Predicate(multiTenantContext))
            await next(context);
        else if (options.RedirectTo is not null)
            context.Response.Redirect(options.RedirectTo.ToString());
    }
}