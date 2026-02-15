namespace AsyncOrders.Application.Abstractions.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string routingKey, IDictionary<string, object>? headers, CancellationToken ct);
}
