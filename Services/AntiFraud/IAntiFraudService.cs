namespace Spotster.Services.AntiFraud;

public class FraudCheckResult
{
    public bool IsAllowed { get; init; }
    public string? DenyReason { get; init; }
    public bool IsSuspicious { get; init; }
    public int SuspiciousScoreDelta { get; init; }
    public string? SuspiciousMessage { get; init; }
}

public interface IAntiFraudService
{
    Task<FraudCheckResult> ValidateReportAsync(
        Guid userId,
        double latitude,
        double longitude,
        string? ipAddress,
        string? userAgent);

    Task RecordSuspiciousActivityAsync(Guid userId, int scoreDelta, string reason);
    string ComputeDeviceFingerprint(string? userAgent, string? ipAddress);
    string ComputeIpHash(string? ipAddress);
    Task<bool> IsUserAllowedAsync(Guid userId);

    Task<Entities.User?> SyncUserStatusAsync(Guid userId);
}
