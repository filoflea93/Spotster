namespace Spotster.Entities;

public class UserReview
{
    public Guid Id { get; set; }
    public Guid ReviewerUserId { get; set; }
    public Guid ReviewedUserId { get; set; }
    public Guid? ParkingRequestId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public User ReviewerUser { get; set; } = null!;
    public User ReviewedUser { get; set; } = null!;
    public ParkingRequest? ParkingRequest { get; set; } = null!;
}
