using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace AsyncOrders.Tests.Integration;

public sealed class ContainersFixture : IAsyncLifetime
{
    public MsSqlContainer Sql { get; }
    public RabbitMqContainer Rabbit { get; }

    public string SqlConnectionString => Sql.GetConnectionString();

    public string RabbitHost => Rabbit.Hostname;
    public int RabbitPort => Rabbit.GetMappedPublicPort(5672);

    // ✅ use esses no teste/config
    public string RabbitUser => "test";
    public string RabbitPass => "test";

    public ContainersFixture()
    {
        Sql = new MsSqlBuilder()
            .WithPassword("Tua_Senha_Super_Secreta_123")
            .Build();

        Rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername(RabbitUser)
            .WithPassword(RabbitPass)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await Sql.StartAsync();
        await Rabbit.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Rabbit.DisposeAsync();
        await Sql.DisposeAsync();
    }
}
