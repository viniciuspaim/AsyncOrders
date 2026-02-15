using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AsyncOrders.Domain.Orders;

namespace AsyncOrders.Infrastructure.Persistence.Configurations;

public sealed class OrderProcessingLogConfiguration : IEntityTypeConfiguration<OrderProcessingLog>
{
    public void Configure(EntityTypeBuilder<OrderProcessingLog> b)
    {
        b.ToTable("OrderProcessingLogs");

        b.HasKey(x => x.Id);

        b.Property(x => x.OrderId)
            .IsRequired();

        b.Property(x => x.CorrelationId)
            .IsRequired()
            .HasMaxLength(64);

        b.HasIndex(x => x.CorrelationId);

        b.Property(x => x.Attempt)
            .IsRequired();

        b.Property(x => x.StartedAtUtc)
            .IsRequired();

        b.Property(x => x.EndedAtUtc);

        b.Property(x => x.Succeeded)
            .IsRequired();

        b.Property(x => x.ErrorMessage)
            .HasMaxLength(1024);
    }
}
