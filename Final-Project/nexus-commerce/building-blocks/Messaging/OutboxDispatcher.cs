using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nexus.BuildingBlocks.Messaging;

/// <summary>Polls the service's outbox and publishes pending messages to RabbitMQ (at-least-once).</summary>
public sealed class OutboxDispatcher<TContext>(IServiceScopeFactory scopeFactory, RabbitMqConnection mq,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext, IMessagingDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give EnsureCreated a moment before first poll.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;
            try
            {
                published = await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox dispatch failed; will retry");
            }
            await Task.Delay(published > 0 ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var batch = await db.OutboxMessages
            .Where(m => m.PublishedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        var count = 0;
        foreach (var m in batch)
        {
            try
            {
                mq.Publish(new EventEnvelope
                {
                    MessageId = m.Id, Type = m.RoutingKey, Version = m.Version,
                    CorrelationId = m.CorrelationId, OccurredAtUtc = m.CreatedAtUtc, Payload = m.Payload
                });
                m.PublishedAtUtc = DateTime.UtcNow;
                count++;
            }
            catch (Exception ex)
            {
                m.Attempts++;
                m.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                logger.LogWarning(ex, "Publishing outbox message {Id} ({Key}) failed (attempt {Attempts})", m.Id, m.RoutingKey, m.Attempts);
                await db.SaveChangesAsync(ct);
                return count; // preserve ordering: stop at first failure
            }
        }
        if (batch.Count > 0) await db.SaveChangesAsync(ct);
        return count;
    }
}
