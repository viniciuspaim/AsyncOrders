using AsyncOrders.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsyncOrders.Infrastructure.Persistence.Repositories;

public sealed class OrderProcessingLogRepository : IOrderProcessingLogRepository
{
    private readonly AppDbContext _db;

    public OrderProcessingLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Domain.Orders.OrderProcessingLog log, CancellationToken ct)
    {
        await _db.OrderProcessingLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> HasSucceededForCorrelationIdAsync(string correlationId, CancellationToken ct)
    {
        return _db.OrderProcessingLogs
            .AsNoTracking()
            .AnyAsync(x => x.CorrelationId == correlationId && x.Succeeded, ct);
    }
}
