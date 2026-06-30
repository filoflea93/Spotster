using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Spotster.Infrastructure;

public static class FormCoordinateParser
{
    public static bool TryParseLatitude(IFormCollection form, out double latitude) =>
        TryParse(form, "latitude", -90, 90, out latitude);

    public static bool TryParseLongitude(IFormCollection form, out double longitude) =>
        TryParse(form, "longitude", -180, 180, out longitude);

    private static bool TryParse(IFormCollection form, string key, double min, double max, out double value)
    {
        value = 0;
        var raw = form[key].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return value >= min && value <= max;
    }
}
