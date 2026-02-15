namespace AsyncOrders.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderResponse(
    Guid OrderId,
    string CorrelationId
);
