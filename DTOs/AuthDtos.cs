namespace Spotster.DTOs;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth);

public record LoginRequest(string Username, string Password);

public record ResendConfirmationRequest(string Email);

public record RegisterResponse(string Message, string Email);

public record AuthResponse(
    Guid UserId,
    string Username,
    int ReputationScore,
    double AccuracyRate,
    string Status,
    DateTime? SuspendedUntil,
    int SuspiciousScore,
    string? ProfilePhotoUrl);

public record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AuthResponse User);

public record RefreshTokenRequest(string RefreshToken);

public record UpdateLocationRequest(double Latitude, double Longitude);
