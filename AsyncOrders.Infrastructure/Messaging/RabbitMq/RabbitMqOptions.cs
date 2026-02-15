namespace AsyncOrders.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string Exchange { get; init; } = "orders.ex";
}