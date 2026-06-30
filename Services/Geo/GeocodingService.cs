using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spotster.DTOs;
using Spotster.Resources;
using Microsoft.Extensions.Localization;

namespace Spotster.Services.Geo;

public interface IGeocodingService
{
    Task<GeocodeResultDto> GeocodeAddressAsync(string address);
    Task<IReadOnlyList<AddressSuggestionDto>> SuggestAddressesAsync(
        string query,
        double? biasLatitude,
        double? biasLongitude,
        int limit = 6);
}

public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _http;
    private readonly ILogger<GeocodingService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GeocodingService(
        HttpClient http,
        ILogger<GeocodingService> logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _http = http;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<GeocodeResultDto> GeocodeAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException(_localizer["Error_AddressRequired"]);
        }

        var url = $"search?format=json&limit=1&q={Uri.EscapeDataString(address.Trim())}";
        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<NominatimResult>>(json) ?? [];

        if (results.Count == 0)
        {
            throw new KeyNotFoundException(_localizer["Error_AddressNotFound"]);
        }

        var hit = results[0];
        if (!double.TryParse(hit.Lat, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(hit.Lon, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            throw new InvalidOperationException(_localizer["Error_GeocodingInvalid"]);
        }

        _logger.LogInformation("Geocoded '{Address}' -> {Lat},{Lng}", address, lat, lng);
        return new GeocodeResultDto(lat, lng, hit.DisplayName ?? address.Trim());
    }

    public async Task<IReadOnlyList<AddressSuggestionDto>> SuggestAddressesAsync(
        string query,
        double? biasLatitude,
        double? biasLongitude,
        int limit = 6)
    {
        query = query.Trim();
        if (query.Length < 3)
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 8);
        var url = $"search?format=json&limit={limit}&q={Uri.EscapeDataString(query)}&addressdetails=1&countrycodes=it";

        if (biasLatitude.HasValue && biasLongitude.HasValue)
        {
            const double delta = 0.08;
            var lat = biasLatitude.Value;
            var lng = biasLongitude.Value;
            var left = lng - delta;
            var right = lng + delta;
            var top = lat + delta;
            var bottom = lat - delta;
            url += $"&viewbox={left.ToString(System.Globalization.CultureInfo.InvariantCulture)},{top.ToString(System.Globalization.CultureInfo.InvariantCulture)},{right.ToString(System.Globalization.CultureInfo.InvariantCulture)},{bottom.ToString(System.Globalization.CultureInfo.InvariantCulture)}&bounded=0";
        }

        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<NominatimResult>>(json) ?? [];

        return results
            .Select(r =>
            {
                if (!double.TryParse(r.Lat, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(r.Lon, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var lng))
                {
                    return null;
                }

                var formatted = r.DisplayName ?? query;
                return new AddressSuggestionDto(lat, lng, formatted, BuildShortLabel(r, formatted));
            })
            .Where(s => s is not null)
            .Cast<AddressSuggestionDto>()
            .ToList();
    }

    private static string BuildShortLabel(NominatimResult result, string fallback)
    {
        if (result.Address is null)
        {
            return Truncate(fallback, 80);
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Address.Road))
        {
            var road = result.Address.Road;
            if (!string.IsNullOrWhiteSpace(result.Address.HouseNumber))
            {
                road += " " + result.Address.HouseNumber;
            }
            parts.Add(road);
        }

        if (!string.IsNullOrWhiteSpace(result.Address.Suburb))
        {
            parts.Add(result.Address.Suburb);
        }
        else if (!string.IsNullOrWhiteSpace(result.Address.City))
        {
            parts.Add(result.Address.City);
        }
        else if (!string.IsNullOrWhiteSpace(result.Address.Town))
        {
            parts.Add(result.Address.Town);
        }

        if (!string.IsNullOrWhiteSpace(result.Address.State))
        {
            parts.Add(result.Address.State);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : Truncate(fallback, 80);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        [JsonPropertyName("road")]
        public string? Road { get; set; }

        [JsonPropertyName("house_number")]
        public string? HouseNumber { get; set; }

        [JsonPropertyName("suburb")]
        public string? Suburb { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }
    }
}

public static class GeocodingServiceRegistration
{
    public static IServiceCollection AddGeocoding(this IServiceCollection services)
    {
        services.AddHttpClient<IGeocodingService, GeocodingService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Spotster", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
