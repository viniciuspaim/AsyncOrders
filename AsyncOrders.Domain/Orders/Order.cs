namespace AsyncOrders.Domain.Orders;

public sealed class Order
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public OrderStatus Status { get; private set; }

    public string CorrelationId { get; private set; } = default!;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    private Order() { } // EF Core

    public Order(Guid id, string customerId, decimal amount, string correlationId, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("CustomerId is required.", nameof(customerId));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be > 0.");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        Id = id;
        CustomerId = customerId.Trim();
        Amount = amount;
        CorrelationId = correlationId.Trim();

        Status = OrderStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void MarkProcessing(DateTime nowUtc)
    {
        EnsureCanTransitionTo(OrderStatus.Processing);
        Status = OrderStatus.Processing;
        UpdatedAtUtc = nowUtc;
        LastError = null;
    }

    public void MarkCompleted(DateTime nowUtc)
    {
        EnsureCanTransitionTo(OrderStatus.Completed);
        Status = OrderStatus.Completed;
        UpdatedAtUtc = nowUtc;
        LastError = null;
    }

    public void MarkFailed(string error, DateTime nowUtc)
    {
        EnsureCanTransitionTo(OrderStatus.Failed);
        Status = OrderStatus.Failed;
        UpdatedAtUtc = nowUtc;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error;
    }

    private void EnsureCanTransitionTo(OrderStatus target)
    {
        var ok =
            (Status == OrderStatus.Pending && target == OrderStatus.Processing) ||
            (Status == OrderStatus.Processing && (target == OrderStatus.Completed || target == OrderStatus.Failed));

        if (!ok)
            throw new InvalidOperationException($"Invalid transition: {Status} -> {target}");
    }
}
