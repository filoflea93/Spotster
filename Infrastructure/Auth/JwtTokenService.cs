using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Spotster.Data;
using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Spotster.Infrastructure.Auth;

public class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly AppDbContext _db;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public JwtTokenService(
        IOptions<JwtSettings> settings,
        AppDbContext db,
        IStringLocalizer<SharedResources> localizer)
    {
        _settings = settings.Value;
        _db = db;
        _localizer = localizer;
    }

    public async Task<AuthTokenResponse> IssueTokenResponseAsync(User user, CancellationToken cancellationToken = default)
    {
        var (accessToken, accessExpires, refreshToken) = await IssueTokensAsync(user, cancellationToken);
        return new AuthTokenResponse(accessToken, refreshToken, accessExpires, ToAuthResponse(user));
    }

    public async Task<AuthTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken, cancellationToken)
            ?? throw new UnauthorizedAccessException(_localizer["Error_InvalidRefreshToken"]);

        if (!stored.IsActive)
        {
            throw new UnauthorizedAccessException(_localizer["Error_RefreshTokenExpiredOrRevoked"]);
        }

        stored.RevokedAt = DateTime.UtcNow;
        var (accessToken, accessExpires, newRefreshToken) = await IssueTokensAsync(stored.User, cancellationToken, stored.Token);
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthTokenResponse(accessToken, newRefreshToken, accessExpires, ToAuthResponse(stored.User));
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string AccessToken, DateTime AccessExpires, string RefreshToken)> IssueTokensAsync(
        User user,
        CancellationToken cancellationToken,
        string? replacedByToken = null)
    {
        var accessExpires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        var accessToken = CreateAccessToken(user, accessExpires);
        var refreshToken = GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
            ReplacedByToken = replacedByToken
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (accessToken, accessExpires, refreshToken);
    }

    private string CreateAccessToken(User user, DateTime expires)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
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
