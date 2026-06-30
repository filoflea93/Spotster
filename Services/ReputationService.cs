using Spotster.Domain.Reputation;
using Spotster.Entities;
using Spotster.Repositories;
using Spotster.Services.Users;

namespace Spotster.Services;

public interface IReputationService
{
    Task ApplyConfirmedValidAsync(Guid userId, int consecutiveReports);
    Task ApplyFalseReportAsync(Guid userId);
    Task ApplyCorrectVoteAsync(Guid userId);
    Task ApplyVoteOutcomesAsync(ParkingReport report);
}

public class ReputationService : IReputationService
{
    private readonly IUserRepository _users;
    private readonly IUserService _userService;

    public ReputationService(IUserRepository users, IUserService userService)
    {
        _users = users;
        _userService = userService;
    }

    public async Task ApplyConfirmedValidAsync(Guid userId, int consecutiveReports)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        user.PositiveReports++;
        user.VerifiedReports++;
        _userService.RecalculateReputation(user);

        var multiplier = ReputationCalculator.GetRewardMultiplier(consecutiveReports);
        var expectedIncrement = ReputationCalculator.VerifiedReportPoints;
        var actualIncrement = (int)Math.Round(expectedIncrement * multiplier);
        user.ReputationScore += actualIncrement - expectedIncrement;

        await _userService.ApplyDailyBonusIfEligibleAsync(user);
        await _users.SaveChangesAsync();
    }

    public async Task ApplyFalseReportAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        user.NegativeReports++;
        user.FalseReports++;
        _userService.RecalculateReputation(user);
        await _users.SaveChangesAsync();
    }

    public async Task ApplyCorrectVoteAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        user.VotesCorrect++;
        _userService.RecalculateReputation(user);
        await _users.SaveChangesAsync();
    }

    public async Task ApplyVoteOutcomesAsync(ParkingReport report)
    {
        var validVotes = report.Votes.Count(v => v.IsValid);
        var invalidVotes = report.Votes.Count(v => !v.IsValid);
        var wasValid = report.Status != ParkingStatus.Invalid && validVotes >= invalidVotes;

        foreach (var vote in report.Votes)
        {
            var voteWasCorrect = wasValid ? vote.IsValid : !vote.IsValid;
            if (voteWasCorrect)
            {
                await ApplyCorrectVoteAsync(vote.UserId);
            }
        }
    }
}
