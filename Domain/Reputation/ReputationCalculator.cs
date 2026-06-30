namespace Spotster.Domain.Reputation;

public static class ReputationCalculator
{
    public const int VerifiedReportPoints = 10;
    public const int FalseReportPenalty = 20;
    public const int CorrectVotePoints = 5;
    public const int DailyBonusPoints = 15;

    public static int CalculateScore(int verifiedReports, int falseReports, int votesCorrect)
    {
        return (verifiedReports * VerifiedReportPoints)
               - (falseReports * FalseReportPenalty)
               + (votesCorrect * CorrectVotePoints);
    }

    public static double CalculateAccuracyRate(int positiveReports, int negativeReports)
    {
        var total = positiveReports + negativeReports;
        if (total == 0)
        {
            return 0;
        }

        return Math.Round((double)positiveReports / total, 2);
    }

    public static double GetRewardMultiplier(int consecutiveReportsInWindow)
    {
        return consecutiveReportsInWindow switch
        {
            <= 1 => 1.0,
            2 => 0.75,
            3 => 0.5,
            _ => 0.25
        };
    }
}
