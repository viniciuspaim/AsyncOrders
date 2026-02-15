namespace AsyncOrders.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderRequest(
    string CustomerId,
    decimal Amount
);
