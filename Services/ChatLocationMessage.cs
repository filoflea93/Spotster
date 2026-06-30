using System.Globalization;
using System.Text.RegularExpressions;

namespace Spotster.Services;

public static class ChatLocationMessage
{
    public const string Prefix = "__SPOTLOC__:";

    private static readonly Regex Pattern = new(
        @"^__SPOTLOC__:([-+]?\d+(?:\.\d+)?),([-+]?\d+(?:\.\d+)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsLocationMessage(string? content) =>
        !string.IsNullOrWhiteSpace(content) && content.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Format(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{latitude:F6},{longitude:F6}");

    public static bool TryParse(string? content, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var match = Pattern.Match(content.Trim());
        if (!match.Success)
        {
            return false;
        }

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
            && latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180;
    }
}
