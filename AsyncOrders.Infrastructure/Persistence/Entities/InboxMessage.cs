namespace AsyncOrders.Infrastructure.Persistence.Entities;

public sealed class InboxMessage
{
    public Guid Id { get; set; } // PK interno

    public string MessageId { get; set; } = default!;      // vem do Rabbit (ou gerado)
    public string CorrelationId { get; set; } = default!;  // x-correlation-id
    public string Type { get; set; } = default!;
    public DateTime ReceivedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
    public string Status { get; set; } = "Processing";     // Processing | Completed | Failed
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}