namespace AsyncOrders.Application.Orders.Events;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    string CorrelationId,
    DateTime CreatedAtUtc
);
