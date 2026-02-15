namespace AsyncOrders.Domain.Orders;

public sealed class OrderProcessingLog
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }

    public string CorrelationId { get; private set; } = default!;
    public int Attempt { get; private set; }

    public DateTime StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }

    public bool Succeeded { get; private set; }
    public string? ErrorMessage { get; private set; }

    private OrderProcessingLog() { } // EF Core

    public OrderProcessingLog(Guid id, Guid orderId, string correlationId, int attempt, DateTime startedAtUtc)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("CorrelationId is required.", nameof(correlationId));
        if (attempt <= 0) throw new ArgumentOutOfRangeException(nameof(attempt), "Attempt must be >= 1.");

        Id = id;
        OrderId = orderId;
        CorrelationId = correlationId.Trim();
        Attempt = attempt;
        StartedAtUtc = startedAtUtc;
    }

    public void MarkSuccess(DateTime endedAtUtc)
    {
        Succeeded = true;
        EndedAtUtc = endedAtUtc;
        ErrorMessage = null;
    }

    public void MarkFailure(string errorMessage, DateTime endedAtUtc)
    {
        Succeeded = false;
        EndedAtUtc = endedAtUtc;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown error" : errorMessage;
    }
}
