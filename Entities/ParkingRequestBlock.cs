namespace Spotster.Entities;

public class ParkingRequestBlock
{
    public Guid Id { get; set; }
    public Guid ParkingRequestId { get; set; }
    public Guid GuestUserId { get; set; }
    public Guid BlockedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public ParkingRequest ParkingRequest { get; set; } = null!;
    public User GuestUser { get; set; } = null!;
}
