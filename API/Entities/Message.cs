namespace API.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";   // text, image, file, system...
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public Guid? ReplyToMessageId { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public Message? ReplyToMessage { get; set; }

    public ICollection<Message> Replies { get; set; } = new List<Message>();
    public ICollection<MessageReceipt> Receipts { get; set; } = new List<MessageReceipt>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}