using Spotster.DTOs;
using Spotster.Infrastructure.Auth;
using Spotster.Services;
using Spotster.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Spotster.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserReviewService _reviewService;
    private readonly IAuthService _authService;

    public UsersController(
        IUserService userService,
        IUserReviewService reviewService,
        IAuthService authService)
    {
        _userService = userService;
        _reviewService = reviewService;
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpGet("leaderboard")]
    public async Task<ActionResult<PagedResult<LeaderboardEntryDto>>> GetLeaderboard(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _userService.GetLeaderboardAsync(page, pageSize);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<UserSearchResultDto>>> SearchUsers(
        [FromQuery] string q,
        [FromQuery] int limit = 10)
    {
        var result = await _reviewService.SearchUsersAsync(User.GetUserId(), q ?? string.Empty, limit);
        return Ok(result);
    }

    [Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await _authService.ChangePasswordAsync(User.GetUserId(), request);
        return NoContent();
    }

    [Authorize]
    [HttpPut("me/location")]
    public async Task<ActionResult<AuthResponse>> SyncLocation([FromBody] UpdateLocationRequest request)
    {
        var profile = await _authService.SyncLocationAsync(User.GetUserId(), request);
        return Ok(profile);
    }

    [Authorize]
    [HttpPost("me/profile-photo")]
    public async Task<ActionResult<ProfilePhotoResponse>> UploadProfilePhoto(IFormFile photo)
    {
        var photoUrl = await _userService.UpdateProfilePhotoAsync(User.GetUserId(), photo);
        return Ok(new ProfilePhotoResponse(photoUrl));
    }

    [Authorize]
    [HttpDelete("me/profile-photo")]
    public async Task<IActionResult> DeleteProfilePhoto()
    {
        await _userService.RemoveProfilePhotoAsync(User.GetUserId());
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}/profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(Guid userId)
    {
        var profile = await _userService.GetProfileAsync(userId);
        return Ok(profile);
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}/reviews/summary")]
    public async Task<ActionResult<UserReviewSummaryDto>> GetReviewSummary(Guid userId)
    {
        var summary = await _reviewService.GetSummaryAsync(userId);
        return Ok(summary);
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}/reviews")]
    public async Task<ActionResult<PagedResult<UserReviewDto>>> GetReviews(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _reviewService.GetReviewsAsync(userId, User.TryGetUserId(), page, pageSize);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("{userId:guid}/reviews/status")]
    public async Task<ActionResult<UserReviewStatusDto>> GetReviewStatus(Guid userId)
    {
        var result = await _reviewService.GetReviewStatusAsync(User.GetUserId(), userId);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{userId:guid}/reviews")]
    public async Task<ActionResult<UserReviewDto>> CreateReview(
        Guid userId,
        [FromBody] CreateUserReviewRequest request)
    {
        var result = await _reviewService.CreateReviewAsync(User.GetUserId(), userId, request);
        return Ok(result);
    }
}
