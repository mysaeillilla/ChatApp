using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageReceipt> MessageReceipts => Set<MessageReceipt>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Users ----------
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(50);
            e.Property(x => x.Email).IsRequired().HasMaxLength(255);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.ProfileImage).HasMaxLength(500);
            e.Property(x => x.IsOnline).HasDefaultValue(false);

            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        // ---------- Conversations ----------
        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("Conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.IsGroup).HasDefaultValue(false);
        });

        // ---------- ConversationMembers ----------
        modelBuilder.Entity<ConversationMember>(e =>
        {
            e.ToTable("ConversationMembers");
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Conversation)
             .WithMany(c => c.Members)
             .HasForeignKey(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
             .WithMany(u => u.ConversationMemberships)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // A user can only be in a conversation once
            e.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        // ---------- Messages ----------
        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("Messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.MessageType).IsRequired().HasMaxLength(20).HasDefaultValue("text");

            e.HasOne(x => x.Conversation)
             .WithMany(c => c.Messages)
             .HasForeignKey(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Sender)
             .WithMany(u => u.SentMessages)
             .HasForeignKey(x => x.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            // Self-reference: reply-to
            e.HasOne(x => x.ReplyToMessage)
             .WithMany(m => m.Replies)
             .HasForeignKey(x => x.ReplyToMessageId)
             .OnDelete(DeleteBehavior.SetNull);

            // Fast "load latest messages in a conversation"
            e.HasIndex(x => new { x.ConversationId, x.SentAt });
            e.HasIndex(x => x.SenderId);
        });

        // ---------- MessageReceipts ----------
        modelBuilder.Entity<MessageReceipt>(e =>
        {
            e.ToTable("MessageReceipts");
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Message)
             .WithMany(m => m.Receipts)
             .HasForeignKey(x => x.MessageId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
             .WithMany(u => u.MessageReceipts)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            // One receipt per user per message
            e.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ReadAt });
        });

        // ---------- Attachments ----------
        modelBuilder.Entity<Attachment>(e =>
        {
            e.ToTable("Attachments");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(255);
            e.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
            e.Property(x => x.FileType).IsRequired().HasMaxLength(100);

            e.HasOne(x => x.Message)
             .WithMany(m => m.Attachments)
             .HasForeignKey(x => x.MessageId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.MessageId);
        });
    }
}