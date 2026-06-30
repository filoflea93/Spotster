namespace Spotster.Entities;

public class ParkingRequestMessage
{
    public Guid Id { get; set; }
    public Guid ParkingRequestId { get; set; }
    public Guid SenderUserId { get; set; }
    /// <summary>Guest user in the private thread (always the non-owner of the listing).</summary>
    public Guid GuestUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ParkingRequest ParkingRequest { get; set; } = null!;
    public User SenderUser { get; set; } = null!;
}
