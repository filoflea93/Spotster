using System.Globalization;
using System.Resources;
using Spotster.Resources;
using Microsoft.AspNetCore.Mvc;

namespace Spotster.Controllers;

[ApiController]
[Route("api/localization")]
public class LocalizationController : ControllerBase
{
    private static readonly ResourceManager ResourceManager =
        new(typeof(SharedResources).FullName!, typeof(SharedResources).Assembly);

    [HttpGet("strings")]
    public ActionResult<Dictionary<string, string>> GetStrings([FromQuery] string? culture)
    {
        var cultureName = NormalizeCulture(culture);
        var cultureInfo = CultureInfo.GetCultureInfo(cultureName);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var resourceSet = ResourceManager.GetResourceSet(cultureInfo, createIfNotExists: true, tryParents: true);
        if (resourceSet is null)
        {
            return Ok(result);
        }

        foreach (System.Collections.DictionaryEntry entry in resourceSet)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                result[key] = value;
            }
        }

        return Ok(result);
    }

    private static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "it";
        }

        culture = culture.Trim().ToLowerInvariant();
        return culture.StartsWith("en", StringComparison.Ordinal) ? "en" : "it";
    }
}
