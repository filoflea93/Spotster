using Spotster.DTOs;
using Spotster.Services;
using Microsoft.AspNetCore.Mvc;

namespace Spotster.Controllers;

/// <summary>Localized UI strings for web and mobile clients.</summary>
[ApiController]
[Route("api/localization")]
[ApiExplorerSettings(GroupName = "Localization")]
public class LocalizationController : ControllerBase
{
    private static readonly System.Resources.ResourceManager ResourceManager =
        new(typeof(Resources.SharedResources).FullName!, typeof(Resources.SharedResources).Assembly);

    /// <summary>All translation keys for a culture (`it` or `en`).</summary>
    [HttpGet("strings")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, string>> GetStrings([FromQuery] string? culture)
    {
        var cultureName = NormalizeCulture(culture);
        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
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
