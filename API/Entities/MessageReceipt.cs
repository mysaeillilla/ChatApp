namespace API.Entities;

public class MessageReceipt
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public Message Message { get; set; } = null!;
    public User User { get; set; } = null!;
}
