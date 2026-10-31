using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Nexus.BuildingBlocks.Messaging;

public delegate Task EventHandlerDelegate(IServiceProvider services, EventEnvelope envelope, CancellationToken ct);

/// <summary>Maps routing keys of a service's queue to typed handlers.</summary>
public sealed class EventHandlerRegistry(string queueName)
{
    private readonly Dictionary<string, EventHandlerDelegate> _handlers = new();
    public string QueueName { get; } = queueName;
    public IReadOnlyDictionary<string, EventHandlerDelegate> Handlers => _handlers;

    public EventHandlerRegistry On<T>(Func<IServiceProvider, T, CancellationToken, Task> handler) where T : class
    {
        _handlers[EventContractAttribute.RoutingKeyFor<T>()] = (sp, env, ct) =>
        {
            var evt = JsonSerializer.Deserialize<T>(env.Payload, JsonDefaults.Options)
                      ?? throw new InvalidOperationException("Event payload was empty.");
            return handler(sp, evt, ct);
        };
        return this;
    }
}

/// <summary>
/// Durable queue consumer with manual acks, inbox-based idempotency (the inbox row is saved atomically with the
/// handler's changes), bounded exponential retries and a dead-letter queue (queue + ".dead").
/// </summary>
public sealed class EventConsumerService<TContext>(IServiceScopeFactory scopeFactory, RabbitMqConnection mq,
    EventHandlerRegistry registry, ILogger<EventConsumerService<TContext>> logger) : BackgroundService
    where TContext : DbContext, IMessagingDbContext
{
    private IModel? _channel;
    private CancellationToken _stopping;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Start();
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("RabbitMQ not ready for queue {Queue}: {Message}. Retrying in 5s.", registry.QueueName, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void Start()
    {
        var o = mq.Options;
        _channel = mq.GetConnection().CreateModel();
        _channel.ExchangeDeclare(o.Exchange, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(registry.QueueName + ".dead", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(registry.QueueName, durable: true, exclusive: false, autoDelete: false);
        foreach (var key in registry.Handlers.Keys) _channel.QueueBind(registry.QueueName, o.Exchange, key);
        _channel.BasicQos(0, 10, false);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;
        _channel.BasicConsume(registry.QueueName, autoAck: false, consumer);
        logger.LogInformation("Consuming queue {Queue} ({Count} routing keys)", registry.QueueName, registry.Handlers.Count);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var ch = ((AsyncEventingBasicConsumer)sender).Model;
        var routingKey = ReadHeader(ea, "x-original-routing-key") ?? ea.RoutingKey;
        try
        {
            await ProcessAsync(routingKey, ea.Body.ToArray(), _stopping);
            ch.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(ch, ea, routingKey, ex);
        }
    }

    private async Task ProcessAsync(string routingKey, byte[] body, CancellationToken ct)
    {
        var env = JsonSerializer.Deserialize<EventEnvelope>(Encoding.UTF8.GetString(body), JsonDefaults.Options)
                  ?? throw new InvalidOperationException("Envelope was empty.");
        if (!registry.Handlers.TryGetValue(routingKey, out var handler))
        {
            logger.LogWarning("No handler for {Key}; acknowledging", routingKey);
            return;
        }

        CorrelationContext.Current = env.CorrelationId;
        using var scope = scopeFactory.CreateScope();
        using var _ = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = env.CorrelationId, ["MessageId"] = env.MessageId });
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        if (await db.InboxMessages.AsNoTracking().AnyAsync(i => i.MessageId == env.MessageId, ct))
        {
            logger.LogInformation("Duplicate message {Id} ignored (already processed)", env.MessageId);
            return;
        }

        db.InboxMessages.Add(new InboxMessage { MessageId = env.MessageId, Type = env.Type, ProcessedAtUtc = DateTime.UtcNow });
        await handler(scope.ServiceProvider, env, ct);
        await db.SaveChangesAsync(ct); // business changes + outbox rows + inbox row commit atomically
        logger.LogInformation("Handled {Key}", routingKey);
    }

    private async Task HandleFailureAsync(IModel ch, BasicDeliverEventArgs ea, string routingKey, Exception ex)
    {
        var retries = int.TryParse(ReadHeader(ea, "x-retry"), out var r) ? r : 0;
        try
        {
            var props = ch.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = ea.BasicProperties?.MessageId;
            props.CorrelationId = ea.BasicProperties?.CorrelationId;
            props.Headers = new Dictionary<string, object>
            {
                ["x-original-routing-key"] = routingKey,
                ["x-retry"] = (retries + 1).ToString()
            };
            if (retries < mq.Options.MaxRetries)
            {
                logger.LogWarning(ex, "Handler for {Key} failed (retry {Retry}/{Max}); re-queueing", routingKey, retries + 1, mq.Options.MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retries)), _stopping);
                ch.BasicPublish("", registry.QueueName, props, ea.Body);
            }
            else
            {
                logger.LogError(ex, "Handler for {Key} failed permanently; moving message to {Queue}.dead", routingKey, registry.QueueName);
                props.Headers["x-error"] = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
                ch.BasicPublish("", registry.QueueName + ".dead", props, ea.Body);
            }
            ch.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception inner)
        {
            logger.LogError(inner, "Could not reschedule failed message; nacking with requeue");
            ch.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private static string? ReadHeader(BasicDeliverEventArgs ea, string name)
    {
        var headers = ea.BasicProperties?.Headers;
        if (headers == null || !headers.TryGetValue(name, out var v) || v == null) return null;
        return v is byte[] b ? Encoding.UTF8.GetString(b) : v.ToString();
    }

    public override void Dispose()
    {
        try { _channel?.Close(); } catch { /* shutting down */ }
        base.Dispose();
    }
}
