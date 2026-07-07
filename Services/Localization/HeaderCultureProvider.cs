using Microsoft.AspNetCore.Localization;

namespace Spotster.Services.Localization;

public class HeaderCultureProvider : IRequestCultureProvider
{
    public const string HeaderName = "X-Culture";

    public Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var culture = httpContext.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(culture))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        culture = culture.Split(',')[0].Trim().ToLowerInvariant();
        if (culture.Length > 2)
        {
            culture = culture[..2];
        }

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }
}
