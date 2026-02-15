using System.Text.Json;
using AsyncOrders.Application.Abstractions.Messaging;
using AsyncOrders.Infrastructure.Persistence.Entities;

namespace AsyncOrders.Infrastructure.Persistence.Outbox;

public sealed class EfOutboxWriter : IOutboxWriter
{
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public EfOutboxWriter(AppDbContext db) => _db = db;

    public Task EnqueueAsync<T>(
        T message,
        string routingKey,
        IDictionary<string, object>? headers,
        DateTime occurredAtUtc,
        CancellationToken ct)
        where T : notnull
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = message.GetType().AssemblyQualifiedName!,
            PayloadJson = JsonSerializer.Serialize(message, JsonOpts),
            RoutingKey = routingKey,
            HeadersJson = headers is null ? "{}" : JsonSerializer.Serialize(headers, JsonOpts),
            OccurredAtUtc = occurredAtUtc
        };

        _db.OutboxMessages.Add(outbox);
        return Task.CompletedTask;
    }
}