using AsyncOrders.Application.Abstractions.Persistence;

namespace AsyncOrders.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public EfUnitOfWork(AppDbContext db) => _db = db;
    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
