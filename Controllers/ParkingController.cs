using Spotster.Domain.Geo;
using Spotster.DTOs;
using Spotster.Infrastructure;
using Spotster.Infrastructure.Auth;
using Spotster.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Spotster.Controllers;

/// <summary>Parking reports, requests, geocoding, voting, and request messaging.</summary>
[ApiController]
[Route("api/parking")]
[ApiExplorerSettings(GroupName = "Parking")]
[EnableRateLimiting("write")]
public class ParkingController : ControllerBase
{
    private readonly IParkingService _parkingService;
    private readonly IParkingRequestService _parkingRequestService;
    private readonly IRequestMessagingService _messagingService;

    public ParkingController(
        IParkingService parkingService,
        IParkingRequestService parkingRequestService,
        IRequestMessagingService messagingService)
    {
        _parkingService = parkingService;
        _parkingRequestService = parkingRequestService;
        _messagingService = messagingService;
    }

    /// <summary>Report free parking with photo (multipart: lat, lng, photo).</summary>
    [Authorize]
    [HttpPost("report")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ParkingReportDto>> Report([FromForm] IFormCollection form)
    {
        if (!FormCoordinateParser.TryParseLatitude(form, out var latitude) ||
            !FormCoordinateParser.TryParseLongitude(form, out var longitude))
        {
            throw new ArgumentException("Error_InvalidCoordinates");
        }

        var photo = form.Files.GetFile("photo");
        var request = new CreateParkingReportRequest(latitude, longitude);
        var result = await _parkingService.CreateReportAsync(
            User.GetUserId(),
            request,
            photo,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        return Ok(result);
    }

    /// <summary>Create a parking search request with address, radius, and optional reward.</summary>
    [Authorize]
    [HttpPost("request")]
    public async Task<ActionResult<ParkingRequestDto>> CreateRequest([FromBody] CreateParkingSearchRequest body)
    {
        var result = await _parkingRequestService.CreateRequestAsync(
            User.GetUserId(),
            body,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        return Ok(result);
    }

    /// <summary>List supported payment methods for optional request rewards.</summary>
    [AllowAnonymous]
    [DisableRateLimiting]
    [HttpGet("payment-methods")]
    public ActionResult<IReadOnlyList<PaymentMethodOptionDto>> GetPaymentMethods() =>
        Ok(_messagingService.GetPaymentMethodOptions());

    /// <summary>List chat threads for a request (owner sees all participants).</summary>
    [Authorize]
    [DisableRateLimiting]
    [HttpGet("requests/{requestId:guid}/conversations")]
    public async Task<ActionResult<IReadOnlyList<RequestConversationDto>>> GetConversations(Guid requestId)
    {
        var result = await _messagingService.GetConversationsAsync(requestId, User.GetUserId());
        return Ok(result);
    }

    /// <summary>Get messages in a request chat thread. Use query `with` for the other participant.</summary>
    [Authorize]
    [DisableRateLimiting]
    [HttpGet("requests/{requestId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<RequestMessageDto>>> GetMessages(
        Guid requestId,
        [FromQuery] Guid? with)
    {
        var result = await _messagingService.GetMessagesAsync(requestId, User.GetUserId(), with);
        return Ok(result);
    }

    /// <summary>Send a text message in a request chat.</summary>
    [Authorize]
    [HttpPost("requests/{requestId:guid}/messages")]
    public async Task<ActionResult<RequestMessageDto>> SendMessage(
        Guid requestId,
        [FromBody] SendRequestMessageDto body)
    {
        var result = await _messagingService.SendMessageAsync(requestId, User.GetUserId(), body.Content, body.ReplyToUserId);
        return Ok(result);
    }

    /// <summary>Send a photo message in a request chat (multipart).</summary>
    [Authorize]
    [HttpPost("requests/{requestId:guid}/messages/photo")]
    public async Task<ActionResult<RequestMessageDto>> SendPhotoMessage(
        Guid requestId,
        IFormFile photo,
        [FromForm] Guid? replyToUserId)
    {
        var result = await _messagingService.SendPhotoMessageAsync(requestId, User.GetUserId(), photo, replyToUserId);
        return Ok(result);
    }

    /// <summary>Resolve an address to coordinates for a parking request.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("geocode")]
    [HttpGet("geocode")]
    public async Task<ActionResult<GeocodeResultDto>> Geocode([FromQuery] string address)
    {
        var result = await _parkingRequestService.GeocodeAsync(address);
        return Ok(result);
    }

    /// <summary>Address autocomplete suggestions (min. 3 characters).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("geocode")]
    [HttpGet("geocode/suggest")]
    public async Task<ActionResult<IReadOnlyList<AddressSuggestionDto>>> SuggestAddresses(
        [FromQuery] string q,
        [FromQuery] double? lat,
        [FromQuery] double? lng)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
        {
            return Ok(Array.Empty<AddressSuggestionDto>());
        }

        var result = await _parkingRequestService.SuggestAddressesAsync(q, lat, lng);
        return Ok(result);
    }

    /// <summary>Extend the expiration time of your parking request.</summary>
    [Authorize]
    [HttpPost("requests/{requestId:guid}/renew")]
    public async Task<ActionResult<ParkingRequestDto>> RenewRequest(Guid requestId)
    {
        var result = await _parkingRequestService.RenewRequestAsync(User.GetUserId(), requestId);
        return Ok(result);
    }

    /// <summary>Mark a request as in negotiation / reserved for a guest user.</summary>
    [Authorize]
    [HttpPost("requests/{requestId:guid}/reserve")]
    public async Task<ActionResult<ParkingRequestDto>> ReserveRequest(
        Guid requestId,
        [FromBody] ReserveParkingRequestDto? body)
    {
        var result = await _parkingRequestService.ReserveRequestAsync(
            User.GetUserId(),
            requestId,
            body?.GuestUserId);
        return Ok(result);
    }

    /// <summary>Cancel negotiation / reservation on your request.</summary>
    [Authorize]
    [HttpPost("requests/{requestId:guid}/unreserve")]
    public async Task<ActionResult<ParkingRequestDto>> UnreserveRequest(Guid requestId)
    {
        var result = await _parkingRequestService.UnreserveRequestAsync(User.GetUserId(), requestId);
        return Ok(result);
    }

    /// <summary>Block a user from messaging on your request.</summary>
    [Authorize]
    [HttpPost("requests/{requestId:guid}/block")]
    public async Task<IActionResult> BlockGuest(
        Guid requestId,
        [FromBody] BlockParkingGuestRequestDto body)
    {
        await _parkingRequestService.BlockGuestAsync(User.GetUserId(), requestId, body.GuestUserId);
        return NoContent();
    }

    /// <summary>Unblock a previously blocked user on your request.</summary>
    [Authorize]
    [HttpPost("requests/{requestId:guid}/unblock")]
    public async Task<IActionResult> UnblockGuest(
        Guid requestId,
        [FromBody] BlockParkingGuestRequestDto body)
    {
        await _parkingRequestService.UnblockGuestAsync(User.GetUserId(), requestId, body.GuestUserId);
        return NoContent();
    }

    /// <summary>Update your parking request (address, radius, reward).</summary>
    [Authorize]
    [HttpPut("requests/{requestId:guid}")]
    public async Task<ActionResult<ParkingRequestDto>> UpdateRequest(
        Guid requestId,
        [FromBody] CreateParkingSearchRequest body)
    {
        var result = await _parkingRequestService.UpdateRequestAsync(User.GetUserId(), requestId, body);
        return Ok(result);
    }

    /// <summary>List active parking requests near a map point.</summary>
    [AllowAnonymous]
    [DisableRateLimiting]
    [HttpGet("requests/nearby")]
    public async Task<ActionResult<IReadOnlyList<ParkingRequestDto>>> GetRequestsNearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radius = GeoConstants.NearbyDefaultRadiusMeters)
    {
        var result = await _parkingRequestService.GetNearbyAsync(lat, lng, radius, User.TryGetUserId());
        return Ok(result);
    }

    /// <summary>List your active parking requests.</summary>
    [Authorize]
    [DisableRateLimiting]
    [HttpGet("requests/mine")]
    public async Task<ActionResult<IReadOnlyList<ParkingRequestDto>>> GetMyRequests()
    {
        var result = await _parkingRequestService.GetMyActiveAsync(User.GetUserId());
        return Ok(result);
    }

    /// <summary>List your active parking reports.</summary>
    [Authorize]
    [DisableRateLimiting]
    [HttpGet("reports/mine")]
    public async Task<ActionResult<IReadOnlyList<ParkingReportDto>>> GetMyReports()
    {
        var result = await _parkingService.GetMyActiveAsync(User.GetUserId());
        return Ok(result);
    }

    /// <summary>List active free-parking reports (global, capped). Prefer GET /nearby for map views.</summary>
    [AllowAnonymous]
    [DisableRateLimiting]
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<ParkingReportDto>>> GetActive()
    {
        var result = await _parkingService.GetActiveAsync();
        return Ok(result);
    }

    /// <summary>List free-parking reports near a map point (paginated).</summary>
    [AllowAnonymous]
    [DisableRateLimiting]
    [HttpGet("nearby")]
    public async Task<ActionResult<PagedResult<ParkingReportDto>>> GetNearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radius = GeoConstants.NearbyDefaultRadiusMeters,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _parkingService.GetNearbyAsync(lat, lng, radius, page, pageSize, User.TryGetUserId());
        return Ok(result);
    }

    /// <summary>Delete your parking report.</summary>
    [Authorize]
    [HttpDelete("report/{reportId:guid}")]
    public async Task<IActionResult> DeleteReport(Guid reportId)
    {
        await _parkingService.DeleteReportAsync(User.GetUserId(), reportId);
        return NoContent();
    }

    /// <summary>Delete your parking request.</summary>
    [Authorize]
    [HttpDelete("requests/{requestId:guid}")]
    public async Task<IActionResult> DeleteRequest(Guid requestId)
    {
        await _parkingRequestService.DeleteRequestAsync(User.GetUserId(), requestId);
        return NoContent();
    }

    /// <summary>Vote confirm or deny on a community parking report.</summary>
    [Authorize]
    [HttpPost("vote")]
    public async Task<ActionResult<ParkingReportDto>> Vote([FromBody] VoteRequest request)
    {
        var result = await _parkingService.VoteAsync(User.GetUserId(), request);
        return Ok(result);
    }
}
