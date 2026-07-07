namespace Spotster.DTOs;

/// <summary>Bootstrap metadata for mobile and third-party API clients.</summary>
public record MobileClientConfigDto(
    string ApiVersion,
    string PublicBaseUrl,
    string SignalRHubPath,
    string OpenApiUrl,
    IReadOnlyList<string> SupportedCultures,
    string DefaultCulture,
    string CultureHeaderName,
    AuthClientConfigDto Auth,
    GeoClientConfigDto Geo,
    RateLimitClientConfigDto RateLimits,
    SignalRClientConfigDto SignalR);

/// <summary>Auth constraints and token lifetimes exposed to clients.</summary>
public record AuthClientConfigDto(
    int MinimumPasswordLength,
    int MinimumAgeYears,
    int AccessTokenMinutes,
    int RefreshTokenDays,
    bool EmailConfirmationRequired);

/// <summary>Map and nearby-search defaults (meters).</summary>
public record GeoClientConfigDto(
    double NearbyDefaultRadiusMeters,
    double NearbyMaxRadiusMeters,
    double ViewRadiusMinMeters,
    double ViewRadiusMaxMeters);

/// <summary>Built-in API rate limits (requests per minute per IP or user).</summary>
public record RateLimitClientConfigDto(
    int AuthPerMinute,
    int GeocodePerMinute,
    int WritePerMinute);

/// <summary>SignalR hub contract for real-time map and chat updates.</summary>
public record SignalRClientConfigDto(
    IReadOnlyList<string> HubMethods,
    IReadOnlyList<string> ServerEvents);
