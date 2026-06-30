using System.Globalization;
using System.Net.Mail;
using System.Text;
using Spotster.Domain.Enums;
using Spotster.Domain.Geo;
using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Infrastructure.Auth;
using Spotster.Infrastructure.Email;
using Spotster.Repositories;
using Spotster.Resources;
using Spotster.Services.AntiFraud;
using Spotster.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Spotster.Services;

public class AuthService : IAuthService
{
    private const int MinimumAgeYears = 18;

    private readonly IUserRepository _users;
    private readonly IUserService _userService;
    private readonly IAntiFraudService _antiFraud;
    private readonly UserManager<User> _userManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IEmailSender _emailSender;
    private readonly AppSettings _appSettings;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AuthService(
        IUserRepository users,
        IUserService userService,
        IAntiFraudService antiFraud,
        UserManager<User> userManager,
        JwtTokenService jwtTokenService,
        IEmailSender emailSender,
        IOptions<AppSettings> appSettings,
        IStringLocalizer<SharedResources> localizer)
    {
        _users = users;
        _userService = userService;
        _antiFraud = antiFraud;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _emailSender = emailSender;
        _appSettings = appSettings.Value;
        _localizer = localizer;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        ValidateRegistrationInput(request);

        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await _users.GetByUsernameAsync(username) is not null)
        {
            throw new InvalidOperationException(_localizer["Error_UsernameTaken"]);
        }

        var normalizedEmail = _userManager.NormalizeEmail(email);
        if (await _users.GetByEmailAsync(normalizedEmail) is not null)
        {
            throw new InvalidOperationException(_localizer["Error_EmailTaken"]);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Status = UserStatus.Active,
            EmailConfirmed = false,
            LastActivityAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _userService.RecalculateReputation(user);

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var message = string.Join(" ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException(message);
        }

        await SendConfirmationEmailAsync(user);

        return new RegisterResponse(_localizer["Auth_RegisterPending"], MaskEmail(email));
    }

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username.Trim());
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException(_localizer["Error_InvalidCredentials"]);
        }

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedAccessException(_localizer["Error_EmailNotConfirmed"]);
        }

        if (user.Status == UserStatus.Banned)
        {
            throw new UnauthorizedAccessException(_localizer["Error_AccountBanned"]);
        }

        user.LastActivityAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();

        user = await _antiFraud.SyncUserStatusAsync(user.Id) ?? user;
        return await _jwtTokenService.IssueTokenResponseAsync(user);
    }

    public Task<AuthTokenResponse> RefreshAsync(string refreshToken) =>
        _jwtTokenService.RefreshAsync(refreshToken);

    public Task LogoutAsync(string refreshToken) =>
        _jwtTokenService.RevokeAsync(refreshToken);

    public async Task<bool> ConfirmEmailAsync(Guid userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            return true;
        }

        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
        return result.Succeeded;
    }

    public async Task ResendConfirmationAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(_localizer["Error_EmailRequired"]);
        }

        var normalizedEmail = _userManager.NormalizeEmail(email.Trim());
        var user = await _users.GetByEmailAsync(normalizedEmail);
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        await SendConfirmationEmailAsync(user);
    }

    public async Task<AuthResponse> GetProfileAsync(Guid userId)
    {
        var user = await _antiFraud.SyncUserStatusAsync(userId)
            ?? throw new UnauthorizedAccessException(_localizer["Error_UserNotFound"]);

        if (user.Status == UserStatus.Banned)
        {
            throw new UnauthorizedAccessException(_localizer["Error_AccountBanned"]);
        }

        return ToAuthResponse(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new ArgumentException(_localizer["Error_CredentialsRequired"]);
        }

        if (request.NewPassword.Length < 6)
        {
            throw new ArgumentException(_localizer["Error_PasswordTooShort"]);
        }

        var user = await _users.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException(_localizer["Error_UserNotFound"]);

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var invalidPassword = result.Errors.Any(e =>
                e.Code.Contains("PasswordMismatch", StringComparison.OrdinalIgnoreCase));
            if (invalidPassword)
            {
                throw new UnauthorizedAccessException(_localizer["Error_InvalidCurrentPassword"]);
            }

            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        user.LastActivityAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();
    }

    public async Task<AuthResponse> SyncLocationAsync(Guid userId, UpdateLocationRequest request)
    {
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            throw new ArgumentException(_localizer["Error_InvalidCoordinates"]);
        }

        var user = await _users.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException(_localizer["Error_UserNotFound"]);

        var hadCorruptHistory = user.LastLatitude is < -90 or > 90
            || user.LastLongitude is < -180 or > 180
            || (user.LastLatitude.HasValue && user.LastLongitude.HasValue &&
                GeoHelper.DistanceMeters(
                    user.LastLatitude.Value,
                    user.LastLongitude.Value,
                    request.Latitude,
                    request.Longitude) > 100_000);

        user.LastLatitude = request.Latitude;
        user.LastLongitude = request.Longitude;
        user.LastLocationAt = DateTime.UtcNow;
        user.LastActivityAt = DateTime.UtcNow;

        if (hadCorruptHistory && user.Status == UserStatus.Suspended)
        {
            user.Status = UserStatus.Active;
            user.SuspendedUntil = null;
            user.SuspiciousScore = 0;
        }
        else if (user.SuspiciousScore > 0)
        {
            user.SuspiciousScore = Math.Max(0, user.SuspiciousScore - 15);
        }

        await _users.SaveChangesAsync();
        return ToAuthResponse(user);
    }

    private void ValidateRegistrationInput(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new ArgumentException(_localizer["Error_RegistrationFieldsRequired"]);
        }

        if (request.Password.Length < 6)
        {
            throw new ArgumentException(_localizer["Error_PasswordTooShort"]);
        }

        if (!IsValidEmail(request.Email.Trim()))
        {
            throw new ArgumentException(_localizer["Error_EmailInvalid"]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth > today)
        {
            throw new ArgumentException(_localizer["Error_InvalidDateOfBirth"]);
        }

        var age = today.Year - request.DateOfBirth.Year;
        if (request.DateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        if (age < MinimumAgeYears)
        {
            throw new ArgumentException(_localizer["Error_MinimumAge", MinimumAgeYears]);
        }
    }

    private async Task SendConfirmationEmailAsync(User user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var baseUrl = _appSettings.PublicBaseUrl.TrimEnd('/');
        var confirmUrl = $"{baseUrl}/api/auth/confirm-email?userId={user.Id}&code={encodedToken}";

        var subject = _localizer["Email_ConfirmSubject"].Value;
        var greeting = _localizer["Email_ConfirmGreeting", user.FirstName].Value;
        var bodyText = _localizer["Email_ConfirmBody"].Value;
        var buttonText = _localizer["Email_ConfirmButton"].Value;
        var footer = _localizer["Email_ConfirmFooter"].Value;

        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1e2a38">
              <h2 style="color:#3DAA6D">Spotster</h2>
              <p>{greeting}</p>
              <p>{bodyText}</p>
              <p style="margin:28px 0">
                <a href="{confirmUrl}" style="background:#3DAA6D;color:#fff;padding:12px 22px;border-radius:10px;text-decoration:none;font-weight:700">{buttonText}</a>
              </p>
              <p style="font-size:12px;color:#64748b">{footer}</p>
              <p style="font-size:11px;color:#94a3b8;word-break:break-all">{confirmUrl}</p>
            </div>
            """;

        await _emailSender.SendAsync(user.Email!, subject, html);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return email.Contains('@');
        }
        catch
        {
            return false;
        }
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return email;
        }

        return $"{email[0]}***{email[(at - 1)..]}";
    }

    private static AuthResponse ToAuthResponse(User user) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.ReputationScore,
            user.AccuracyRate,
            user.Status.ToString(),
            user.SuspendedUntil,
            user.SuspiciousScore,
            user.ProfilePhotoUrl);
}
