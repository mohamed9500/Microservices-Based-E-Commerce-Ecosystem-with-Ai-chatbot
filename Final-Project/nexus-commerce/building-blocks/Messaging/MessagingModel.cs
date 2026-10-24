using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts;
using System.Text.Json;

namespace Nexus.BuildingBlocks.Messaging;

/// <summary>Versioned envelope placed on the bus. Payload is the JSON of the contract record.</summary>
public sealed class EventEnvelope
{
    public Guid MessageId { get; set; }
    public string Type { get; set; } = "";          // routing key e.g. order.created.v1
    public int Version { get; set; }
    public string CorrelationId { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
    public string Payload { get; set; } = "";
}

/// <summary>Transactional outbox row: written in the same SaveChanges as the business change.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string RoutingKey { get; set; } = "";
    public int Version { get; set; }
    public string CorrelationId { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Inbox row: guarantees each message is applied at most once per consuming service.</summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string Type { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
}

public interface IMessagingDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }
}

public static class MessagingModelExtensions
{
    /// <summary>Call from OnModelCreating of every messaging-enabled DbContext.</summary>
    public static ModelBuilder ApplyMessagingModel(this ModelBuilder b)
    {
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.RoutingKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            e.Property(x => x.Payload).IsRequired();
            e.HasIndex(x => new { x.PublishedAtUtc, x.CreatedAtUtc });
        });
        b.Entity<InboxMessage>(e =>
        {
            e.ToTable("InboxMessages");
            e.HasKey(x => x.MessageId);
            e.Property(x => x.Type).HasMaxLength(100);
        });
        return b;
    }

    /// <summary>Adds an integration event to the outbox. It is published after the caller's SaveChanges commits.</summary>
    public static void EnqueueEvent<T>(this IMessagingDbContext db, T evt) where T : class
    {
        var c = EventContractAttribute.For<T>();
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            RoutingKey = c.RoutingKey,
            Version = c.Version,
            CorrelationId = CorrelationContext.Current ?? Guid.NewGuid().ToString("N"),
            Payload = JsonSerializer.Serialize(evt, JsonDefaults.Options),
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
