using Spotster.DTOs;

namespace Spotster.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<AuthTokenResponse> LoginAsync(LoginRequest request);
    Task<AuthTokenResponse> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<bool> ConfirmEmailAsync(Guid userId, string code);
    Task ResendConfirmationAsync(string email);
    Task<AuthResponse> GetProfileAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<AuthResponse> SyncLocationAsync(Guid userId, UpdateLocationRequest request);
}
