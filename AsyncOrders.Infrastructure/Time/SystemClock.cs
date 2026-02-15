using AsyncOrders.Application.Abstractions.Time;

namespace AsyncOrders.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
