namespace Spotster.Entities;

using NetTopologySuite.Geometries;

public class ParkingReport
{
    public Guid Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Point Location { get; set; } = null!;
    public string VirtualZoneKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ParkingStatus Status { get; set; }
    public string? PhotoUrl { get; set; }
    public double ConfidenceScore { get; set; } = 50;
    public int ReportCount { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<ReportVote> Votes { get; set; } = new List<ReportVote>();
}
