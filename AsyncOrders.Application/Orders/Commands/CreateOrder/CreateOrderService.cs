using AsyncOrders.Application.Abstractions.Persistence;
using AsyncOrders.Application.Abstractions.Messaging;
using AsyncOrders.Application.Abstractions.Time;
using AsyncOrders.Application.Orders.Events;
using AsyncOrders.Domain.Orders;
using FluentValidation;

namespace AsyncOrders.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IOutboxWriter _outbox;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IValidator<CreateOrderRequest> _validator;

    public CreateOrderService(
        IOrderRepository orders,
        IOutboxWriter outbox,
        IUnitOfWork uow,
        IClock clock,
        IValidator<CreateOrderRequest> validator)
    {
        _orders = orders;
        _outbox = outbox;
        _uow = uow;
        _clock = clock;
        _validator = validator;
    }

    public async Task<CreateOrderResponse> ExecuteAsync(CreateOrderRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var now = _clock.UtcNow;

        var orderId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString("N");

        var order = new Order(
            id: orderId,
            customerId: request.CustomerId,
            amount: request.Amount,
            correlationId: correlationId,
            createdAtUtc: now
        );

        await _orders.AddAsync(order, ct);

        var evt = new OrderCreatedEvent(orderId, correlationId, now);

        var headers = new Dictionary<string, object>
        {
            ["x-correlation-id"] = correlationId,
            ["x-attempt"] = 1
        };

        await _outbox.EnqueueAsync(
            message: evt,
            routingKey: "orders.created",
            headers: headers,
            occurredAtUtc: now,
            ct: ct);

        // Order + Outbox na mesma transação (mesmo DbContext)
        await _uow.SaveChangesAsync(ct);

        return new CreateOrderResponse(orderId, correlationId);
    }
}