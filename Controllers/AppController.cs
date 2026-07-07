using Spotster.Domain.Geo;
using Spotster.DTOs;
using Spotster.Infrastructure.Auth;
using Spotster.Infrastructure.Email;
using Spotster.Services.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Spotster.Controllers;

/// <summary>Client bootstrap and API discovery for mobile apps.</summary>
[ApiController]
[Route("api/app")]
[ApiExplorerSettings(GroupName = "App")]
public class AppController : ControllerBase
{
    private const int MinimumAgeYears = 18;

    /// <summary>
    /// Returns API version, public URLs, auth/geo limits, rate limits, and SignalR event names.
    /// Call once at app startup before login.
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(MobileClientConfigDto), StatusCodes.Status200OK)]
    public ActionResult<MobileClientConfigDto> GetClientConfig(
        [FromServices] IOptions<AppSettings> appSettings,
        [FromServices] IOptions<JwtSettings> jwtSettings)
    {
        var baseUrl = appSettings.Value.PublicBaseUrl.TrimEnd('/');
        var config = new MobileClientConfigDto(
            ApiVersion: "1.0",
            PublicBaseUrl: baseUrl,
            SignalRHubPath: "/hubs/parking",
            OpenApiUrl: $"{baseUrl}/swagger/v1/swagger.json",
            SupportedCultures: new[] { "it", "en" },
            DefaultCulture: "it",
            CultureHeaderName: HeaderCultureProvider.HeaderName,
            Auth: new AuthClientConfigDto(
                MinimumPasswordLength: 6,
                MinimumAgeYears: MinimumAgeYears,
                AccessTokenMinutes: jwtSettings.Value.AccessTokenMinutes,
                RefreshTokenDays: jwtSettings.Value.RefreshTokenDays,
                EmailConfirmationRequired: true),
            Geo: new GeoClientConfigDto(
                NearbyDefaultRadiusMeters: GeoConstants.NearbyDefaultRadiusMeters,
                NearbyMaxRadiusMeters: GeoConstants.NearbyMaxRadiusMeters,
                ViewRadiusMinMeters: 500,
                ViewRadiusMaxMeters: GeoConstants.NearbyMaxRadiusMeters),
            RateLimits: new RateLimitClientConfigDto(
                AuthPerMinute: 20,
                GeocodePerMinute: 40,
                WritePerMinute: 120),
            SignalR: new SignalRClientConfigDto(
                HubMethods: new[]
                {
                    "SetMapViewport",
                    "JoinRequestChat",
                    "LeaveRequestChat"
                },
                ServerEvents: new[]
                {
                    "ParkingCreated",
                    "ParkingUpdated",
                    "ParkingExpired",
                    "ParkingRequestCreated",
                    "ParkingRequestRenewed",
                    "ParkingRequestUpdated",
                    "ParkingRequestExpired",
                    "ParkingRequestGuestBlocked",
                    "ParkingRequestGuestUnblocked",
                    "ParkingSpottedNearRequest",
                    "RequestMessageReceived",
                    "AccountRatingUpdated",
                    "UserSuspiciousActivity"
                }));

        return Ok(config);
    }
}
