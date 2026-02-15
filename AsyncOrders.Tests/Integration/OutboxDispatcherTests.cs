using System.Text;
using AsyncOrders.Api.Messaging;
using AsyncOrders.Infrastructure.Persistence;
using AsyncOrders.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace AsyncOrders.Tests.Integration;

[Collection("containers")]
public class OutboxDispatcherTests
{
    private readonly ContainersFixture _fx;

    public OutboxDispatcherTests(ContainersFixture fx) => _fx = fx;

    [Fact]
    public async Task Dispatcher_Should_Publish_And_Mark_Processed()
    {
        // -------------------------
        // DI mínimo
        // -------------------------
        var services = new ServiceCollection();

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = _fx.RabbitHost,
                ["RabbitMq:Port"] = _fx.RabbitPort.ToString(),
                ["RabbitMq:User"] = _fx.RabbitUser,
                ["RabbitMq:Password"] = _fx.RabbitPass
            })
            .Build();

        services.AddSingleton<IConfiguration>(cfg);

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(_fx.SqlConnectionString));

        var sp = services.BuildServiceProvider();

        // -------------------------
        // Garante topologia no Rabbit (exchange usado pelo dispatcher)
        // -------------------------
        await EnsureRabbitExchangeAsync(cfg);

        // -------------------------
        // Aplica migrations + cria 1 msg de outbox
        // -------------------------
        var msgId = Guid.NewGuid();

        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = msgId,
                Type = "TestEvent",
                PayloadJson = "{\"x\":1}",
                RoutingKey = "orders.created",
                HeadersJson = "{\"x-correlation-id\":\"abc\",\"x-attempt\":1}",
                OccurredAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // -------------------------
        // Start dispatcher
        // -------------------------
        var dispatcher = new OutboxDispatcher(sp, cfg);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await dispatcher.StartAsync(cts.Token);

        // -------------------------
        // Espera (polling) até ProcessedAtUtc ser preenchido
        // -------------------------
        await WaitUntilAsync(async () =>
        {
            await using var scope = sp.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var msg = await db.OutboxMessages.FirstAsync(x => x.Id == msgId);
            return msg.ProcessedAtUtc is not null;
        }, timeout: TimeSpan.FromSeconds(8), pollEvery: TimeSpan.FromMilliseconds(200));

        // Stop dispatcher
        await dispatcher.StopAsync(CancellationToken.None);

        // Assert final (recarrega)
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var msg = await db.OutboxMessages.FirstAsync(x => x.Id == msgId);

            msg.ProcessedAtUtc.Should().NotBeNull();
        }
    }

    private static async Task EnsureRabbitExchangeAsync(IConfiguration cfg)
    {
        var host = cfg["RabbitMq:Host"] ?? "localhost";
        var port = int.TryParse(cfg["RabbitMq:Port"], out var p) ? p : 5672;
        var user = cfg["RabbitMq:User"] ?? "guest";
        var pass = cfg["RabbitMq:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = pass
        };

        await using var conn = await factory.CreateConnectionAsync();
        await using var ch = await conn.CreateChannelAsync();

        // Exchange que o OutboxDispatcher usa
        await ch.ExchangeDeclareAsync(
            exchange: "orders.ex",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan pollEvery)
    {
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (await condition())
                return;

            await Task.Delay(pollEvery);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }
}