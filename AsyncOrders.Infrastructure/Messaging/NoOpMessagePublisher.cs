using AsyncOrders.Application.Abstractions.Messaging;

namespace AsyncOrders.Infrastructure.Messaging;

public sealed class NoOpMessagePublisher : IMessagePublisher
{
    public Task PublishAsync<T>(T message, string routingKey, IDictionary<string, object>? headers, CancellationToken ct)
        => Task.CompletedTask;
}
