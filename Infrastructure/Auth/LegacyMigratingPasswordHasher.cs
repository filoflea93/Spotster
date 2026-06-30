using Spotster.Data;
using Spotster.Entities;
using Microsoft.AspNetCore.Identity;

namespace Spotster.Infrastructure.Auth;

/// <summary>
/// Accepts legacy PBKDF2 hashes (salt.hash format) and migrates them to Identity format on login.
/// </summary>
public class LegacyMigratingPasswordHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> _identityHasher = new();

    public string HashPassword(User user, string password) =>
        _identityHasher.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        if (hashedPassword.Contains('.'))
        {
            return PasswordHasher.Verify(providedPassword, hashedPassword)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        return _identityHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
