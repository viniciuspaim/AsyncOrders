using System.Text.Json;
using AsyncOrders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace AsyncOrders.Api.Messaging;

public sealed class OutboxDispatcher : BackgroundService
{
    private const string Exchange = "orders.ex";

    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;

    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public OutboxDispatcher(IServiceProvider sp, IConfiguration cfg)
    {
        _sp = sp;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _cfg["RabbitMq:Host"] ?? "localhost",
            Port = int.TryParse(_cfg["RabbitMq:Port"], out var p) ? p : 5672,
            UserName = _cfg["RabbitMq:User"] ?? "guest",
            Password = _cfg["RabbitMq:Password"] ?? "guest"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Garante que o exchange existe (não depende do worker ter criado)
        await _channel.ExchangeDeclareAsync(
            exchange: Exchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var messages = await db.OutboxMessages
                    .Where(x => x.ProcessedAtUtc == null)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var msg in messages)
                {
                    try
                    {
                        var body = JsonSerializer.SerializeToUtf8Bytes(
                            JsonDocument.Parse(msg.PayloadJson).RootElement,
                            JsonOpts);

                        var props = new BasicProperties
                        {
                            ContentType = "application/json",
                            DeliveryMode = DeliveryModes.Persistent,
                            Headers = DeserializeHeaders(msg.HeadersJson)
                        };

                        await _channel.BasicPublishAsync(
                            exchange: Exchange,
                            routingKey: msg.RoutingKey,
                            mandatory: false,
                            basicProperties: props,
                            body: body,
                            cancellationToken: stoppingToken);

                        msg.ProcessedAtUtc = DateTime.UtcNow;
                        msg.LastError = null;
                    }
                    catch (Exception ex)
                    {
                        msg.Attempts++;
                        msg.LastError = ex.Message;
                        // não marca ProcessedAtUtc
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch
            {
                // erro global do loop: não derruba o dispatcher
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private static IDictionary<string, object?>? DeserializeHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
            return null;

        try
        {
            // Armazena headers como Dictionary<string, object> no JSON.
            // Ao desserializar, o System.Text.Json tende a virar JsonElement.
            // Rabbit aceita object, então mantemos JsonElement ou convertemos para string/number.

            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headersJson, JsonOpts);
            if (dict is null) return null;

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (k, v) in dict)
            {
                result[k] = v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Number => v.TryGetInt64(out var l) ? l : v.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => v.ToString()
                };
            }

            return result;
        }
        catch
        {
            // Se headers quebrados, não impede publish
            return null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);

        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
