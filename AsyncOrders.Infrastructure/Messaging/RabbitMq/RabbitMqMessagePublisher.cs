using AsyncOrders.Application.Abstractions.Messaging;
using System.Text.Json;
using RabbitMQ.Client;
using System.Text;

namespace AsyncOrders.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _opt;

    // Inicialização async “lazy”
    private readonly Lazy<Task<(IConnection conn, IChannel ch)>> _lazy;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public RabbitMqMessagePublisher(RabbitMqOptions opt)
    {
        _opt = opt;
        _lazy = new Lazy<Task<(IConnection, IChannel)>>(InitAsync);
    }

    private async Task<(IConnection conn, IChannel ch)> InitAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _opt.Host,
            Port = _opt.Port,
            UserName = _opt.User,
            Password = _opt.Password
        };

        var conn = await factory.CreateConnectionAsync();
        var ch = await conn.CreateChannelAsync();

        // Exchange + filas/bindings
        await ch.ExchangeDeclareAsync(exchange: _opt.Exchange, type: ExchangeType.Direct, durable: true, autoDelete: false);

        await ch.QueueDeclareAsync(queue: "orders.created.q", durable: true, exclusive: false, autoDelete: false, arguments: null);
        await ch.QueueBindAsync(queue: "orders.created.q", exchange: _opt.Exchange, routingKey: "orders.created", arguments: null);

        await ch.QueueDeclareAsync(queue: "orders.created.dlq.q", durable: true, exclusive: false, autoDelete: false, arguments: null);
        await ch.QueueBindAsync(queue: "orders.created.dlq.q", exchange: _opt.Exchange, routingKey: "orders.created.dlq", arguments: null);

        return (conn, ch);
    }

    public async Task PublishAsync<T>(T message, string routingKey, IDictionary<string, object>? headers, CancellationToken ct)
    {
        var (conn, ch) = await _lazy.Value;

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOpts));

        // v7: Headers é IDictionary<string, object?>
        IDictionary<string, object?>? typedHeaders = null;
        if (headers is not null)
        {
            typedHeaders = new Dictionary<string, object?>(headers.Count);
            foreach (var kv in headers)
                typedHeaders[kv.Key] = kv.Value;
        }

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = typedHeaders
        };

        await ch.BasicPublishAsync(
            exchange: _opt.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_lazy.IsValueCreated) return;

        var (conn, ch) = await _lazy.Value;

        try { await ch.CloseAsync(); } catch { }
        try { await conn.CloseAsync(); } catch { }

        try { await ch.DisposeAsync(); } catch { }
        try { await conn.DisposeAsync(); } catch { }
    }
}