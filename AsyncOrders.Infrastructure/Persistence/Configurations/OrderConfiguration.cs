using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AsyncOrders.Domain.Orders;

namespace AsyncOrders.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");

        b.HasKey(x => x.Id);

        b.Property(x => x.CustomerId)
            .IsRequired()
            .HasMaxLength(64);

        b.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        b.Property(x => x.Status)
            .IsRequired();

        b.Property(x => x.CorrelationId)
            .IsRequired()
            .HasMaxLength(64);

        b.HasIndex(x => x.CorrelationId)
            .IsUnique();

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        b.Property(x => x.LastError)
            .HasMaxLength(1024);
    }
}
