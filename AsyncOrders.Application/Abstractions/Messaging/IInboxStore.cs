namespace AsyncOrders.Application.Abstractions.Messaging;

public interface IInboxStore
{
    Task<bool> HasProcessedAsync(string messageId, string type, CancellationToken ct);

    Task StartProcessingAsync(string messageId, string correlationId, string type, DateTime receivedAtUtc, CancellationToken ct);

    Task MarkCompletedAsync(string messageId, string type, DateTime processedAtUtc, CancellationToken ct);

    Task MarkFailedAsync(string messageId, string type, string error, DateTime failedAtUtc, CancellationToken ct);
}
