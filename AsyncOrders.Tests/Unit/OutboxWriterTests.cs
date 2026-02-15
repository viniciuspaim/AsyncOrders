using AsyncOrders.Infrastructure.Persistence;
using AsyncOrders.Infrastructure.Persistence.Entities;
using AsyncOrders.Infrastructure.Persistence.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsyncOrders.Tests.Unit;

public class OutboxWriterTests
{
    [Fact]
    public async Task EnqueueAsync_Should_Add_OutboxMessage_To_ChangeTracker()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var sut = new EfOutboxWriter(db);

        await sut.EnqueueAsync(
            message: new { Hello = "World" },
            routingKey: "orders.created",
            headers: new Dictionary<string, object> { ["x-correlation-id"] = "abc" },
            occurredAtUtc: DateTime.UtcNow,
            ct: CancellationToken.None);

        //não precisa SaveChanges: valida que entrou no contexto
        db.ChangeTracker.Entries<OutboxMessage>().Should().HaveCount(1);
    }
}
