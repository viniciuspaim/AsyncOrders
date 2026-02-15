using AsyncOrders.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using AsyncOrders.Domain.Orders;

namespace AsyncOrders.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _db.Orders.AddAsync(order, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return _db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct)
    {
        _db.Orders.Update(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        OrderStatus? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Order> q = _db.Orders.AsNoTracking();

        if (status is not null)
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
