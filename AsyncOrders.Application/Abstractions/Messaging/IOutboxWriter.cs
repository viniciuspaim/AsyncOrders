namespace AsyncOrders.Application.Abstractions.Messaging;

public interface IOutboxWriter
{
    Task EnqueueAsync<T>(
        T message,
        string routingKey,
        IDictionary<string, object>? headers,
        DateTime occurredAtUtc,
        CancellationToken ct)
        where T : notnull;
}