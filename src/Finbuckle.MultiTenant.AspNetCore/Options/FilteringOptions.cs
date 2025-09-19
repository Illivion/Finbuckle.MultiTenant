using Microsoft.AspNetCore.Http;

namespace Finbuckle.MultiTenant.AspNetCore.Options;

public class FilteringOptions
{
    public Func<HttpContext, Boolean>? FilterDelegate { get; set; }
}