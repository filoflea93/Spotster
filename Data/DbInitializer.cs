using Spotster.Domain;
using Spotster.Domain.Enums;
using Spotster.Domain.Reputation;
using Spotster.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Spotster.Data;

public static class DbInitializer
{
    private static readonly Guid Demo1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Demo2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Demo3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(AppDbContext db, UserManager<User> userManager)
    {
        await EnsureDemoUsersAsync(db, userManager);
        await RemoveLegacyDemoSeedReportsAsync(db);
    }

    private static async Task RemoveLegacyDemoSeedReportsAsync(AppDbContext db)
    {
        var demoUserIds = new[] { Demo1Id, Demo2Id, Demo3Id };
        var legacy = await db.ParkingReports
            .Where(r =>
                demoUserIds.Contains(r.CreatedByUserId) &&
                r.ReportCount == 1 &&
                r.ConfidenceScore == 55 &&
                string.IsNullOrEmpty(r.PhotoUrl))
            .ToListAsync();

        if (legacy.Count == 0)
        {
            return;
        }

        db.ParkingReports.RemoveRange(legacy);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoUsersAsync(AppDbContext db, UserManager<User> userManager)
    {
        var now = DateTime.UtcNow;
        var demos = new[]
        {
            (Demo1Id, "demo1", 5, 5, 2),
            (Demo2Id, "demo2", 3, 3, 1),
            (Demo3Id, "demo3", 0, 0, 0)
        };

        foreach (var (id, username, positive, verified, votesCorrect) in demos)
        {
            var existing = await db.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (existing is not null)
            {
                await EnsureDemoProfileAsync(existing, userManager, username);
                continue;
            }

            var email = $"{username}@demo.spotster.local";
            var user = new User
            {
                Id = id,
                UserName = username,
                Email = email,
                NormalizedEmail = userManager.NormalizeEmail(email),
                EmailConfirmed = true,
                FirstName = "Demo",
                LastName = char.ToUpper(username[0]) + username[1..],
                DateOfBirth = new DateOnly(1990, 1, 15),
                ReputationScore = ReputationCalculator.CalculateScore(positive, 0, votesCorrect),
                PositiveReports = positive,
                VerifiedReports = verified,
                VotesCorrect = votesCorrect,
                AccuracyRate = positive > 0 ? 1.0 : 0,
                Status = UserStatus.Active,
                LastActivityAt = now,
                CreatedAt = now
            };

            var result = await userManager.CreateAsync(user, "demo123");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create demo user {username}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }

    private static async Task EnsureDemoProfileAsync(User user, UserManager<User> userManager, string username)
    {
        var email = $"{username}@demo.spotster.local";
        var changed = false;

        if (string.IsNullOrEmpty(user.Email))
        {
            user.Email = email;
            user.NormalizedEmail = userManager.NormalizeEmail(email);
            changed = true;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            changed = true;
        }

        if (string.IsNullOrEmpty(user.FirstName))
        {
            user.FirstName = "Demo";
            changed = true;
        }

        if (string.IsNullOrEmpty(user.LastName))
        {
            user.LastName = char.ToUpper(username[0]) + username[1..];
            changed = true;
        }

        if (user.DateOfBirth == default)
        {
            user.DateOfBirth = new DateOnly(1990, 1, 15);
            changed = true;
        }

        if (changed)
        {
            await userManager.UpdateAsync(user);
        }
    }
}
