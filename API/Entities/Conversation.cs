namespace API.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public string? Name { get; set; }          // null for 1:1 chats
    public bool IsGroup { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}