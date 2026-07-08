namespace Spotster.Domain;

public static class ListingConstants
{
    public const int TtlMinutes = 60;
    public const int ReservationExtensionMinutes = 60;
    public const int MaxRequestRenewals = 1;
    public const int MaxActiveReportsPerUser = 3;
    public const int MaxActiveRequestsPerUser = 1;
    public const int MaxRequestsPerDay = 1;
    /// <summary>Upper bound for global active-list endpoints and cache refresh.</summary>
    public const int MaxActiveListSize = 500;
}
