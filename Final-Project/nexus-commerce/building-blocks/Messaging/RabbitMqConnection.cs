using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.BuildingBlocks.Web;
using RabbitMQ.Client;

namespace Nexus.BuildingBlocks.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "nexus.events";
    public int MaxRetries { get; set; } = 3;
}

/// <summary>Shared connection + a confirm-mode publishing channel.</summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger) : IDisposable
{
    private readonly RabbitMqOptions _o = options.Value;
    private readonly object _lock = new();
    private IConnection? _connection;
    private IModel? _publishChannel;

    public RabbitMqOptions Options => _o;

    public IConnection GetConnection()
    {
        lock (_lock)
        {
            if (_connection is { IsOpen: true }) return _connection;
            var factory = new ConnectionFactory
            {
                HostName = _o.Host,
                Port = _o.Port,
                UserName = _o.User,
                Password = _o.Password,
                VirtualHost = _o.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                ClientProvidedName = "nexus-commerce"
            };
            _connection = factory.CreateConnection();
            logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", _o.Host, _o.Port);
            return _connection;
        }
    }

    public void Publish(EventEnvelope env)
    {
        lock (_lock)
        {
            var conn = GetConnection();
            if (_publishChannel is not { IsOpen: true })
            {
                _publishChannel = conn.CreateModel();
                _publishChannel.ExchangeDeclare(_o.Exchange, ExchangeType.Topic, durable: true);
                _publishChannel.ConfirmSelect();
            }
            var props = _publishChannel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = env.MessageId.ToString();
            props.CorrelationId = env.CorrelationId;
            props.Type = env.Type;
            props.ContentType = "application/json";
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(env, JsonDefaults.Options));
            _publishChannel.BasicPublish(_o.Exchange, env.Type, props, body);
            _publishChannel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        }
    }

    public void Dispose()
    {
        try { _publishChannel?.Close(); } catch { /* shutting down */ }
        try { _connection?.Close(); } catch { /* shutting down */ }
    }
}
