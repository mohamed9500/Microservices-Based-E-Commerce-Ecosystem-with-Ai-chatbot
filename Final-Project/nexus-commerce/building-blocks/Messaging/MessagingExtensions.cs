using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Nexus.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Wires the outbox dispatcher and (optionally) a durable queue consumer for the service's DbContext.
    /// Set Messaging:Enabled=false to disable all RabbitMQ background work (unit/integration tests).
    /// </summary>
    public static IServiceCollection AddMessaging<TContext>(this IServiceCollection services, IConfiguration config,
        string? queueName = null, Action<EventHandlerRegistry>? configure = null)
        where TContext : DbContext, IMessagingDbContext
    {
        if (!config.GetValue("Messaging:Enabled", true)) return services;

        services.Configure<RabbitMqOptions>(config.GetSection("RabbitMq"));
        services.TryAddSingleton<RabbitMqConnection>();
        services.AddHostedService<OutboxDispatcher<TContext>>();

        if (configure != null)
        {
            var registry = new EventHandlerRegistry(queueName ?? throw new ArgumentNullException(nameof(queueName)));
            configure(registry);
            services.AddSingleton(registry);
            services.AddHostedService<EventConsumerService<TContext>>();
        }
        return services;
    }
}
