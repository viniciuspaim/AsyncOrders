namespace AsyncOrders.Infrastructure.Persistence.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string Type { get; set; } = default!;

    public string PayloadJson { get; set; } = default!;

    public string RoutingKey { get; set; } = default!;

    public string HeadersJson { get; set; } = default!;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int Attempts { get; set; }
    public string? LastError { get; set; }

}
