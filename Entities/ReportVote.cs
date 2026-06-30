namespace Spotster.Entities;

public class ReportVote
{
    public Guid Id { get; set; }
    public Guid ParkingReportId { get; set; }
    public Guid UserId { get; set; }
    public bool IsValid { get; set; }
    public DateTime CreatedAt { get; set; }

    public ParkingReport ParkingReport { get; set; } = null!;
    public User User { get; set; } = null!;
}
