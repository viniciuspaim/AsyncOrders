using AsyncOrders.Domain.Orders;

namespace AsyncOrders.Application.Abstractions.Persistence;

public interface IOrderProcessingLogRepository
{
    Task AddAsync(OrderProcessingLog log, CancellationToken ct);

    /// <summary>
    /// Idempotência: retorna true se já existe processamento com sucesso para este CorrelationId.
    /// </summary>
    Task<bool> HasSucceededForCorrelationIdAsync(string correlationId, CancellationToken ct);
}
