namespace Spotster.Entities;

using NetTopologySuite.Geometries;

public class ParkingRequest
{
    public Guid Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Point Location { get; set; } = null!;
    public string Address { get; set; } = string.Empty;
    public int RadiusMeters { get; set; }
    public string VirtualZoneKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ParkingRequestStatus Status { get; set; }
    public int RenewalCount { get; set; }
    public decimal? RewardAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string PaymentMethodsJson { get; set; } = "[]";
    public Guid CreatedByUserId { get; set; }
    public Guid? ReservedByUserId { get; set; }
    public DateTime? ReservedAt { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public User? ReservedByUser { get; set; }
    public ICollection<ParkingRequestMessage> Messages { get; set; } = new List<ParkingRequestMessage>();
    public ICollection<UserReview> Reviews { get; set; } = new List<UserReview>();
    public ICollection<ParkingRequestBlock> Blocks { get; set; } = new List<ParkingRequestBlock>();
}
