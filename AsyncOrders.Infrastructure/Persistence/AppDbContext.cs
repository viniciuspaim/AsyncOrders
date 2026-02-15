using AsyncOrders.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using AsyncOrders.Domain.Orders;

namespace AsyncOrders.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderProcessingLog> OrderProcessingLogs => Set<OrderProcessingLog>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.PayloadJson).IsRequired();
            b.Property(x => x.RoutingKey).IsRequired();
            b.Property(x => x.HeadersJson).IsRequired();
            b.Property(x => x.Attempts).HasDefaultValue(0);
            b.Property(x => x.LastError).HasMaxLength(4000);
            b.HasIndex(x => x.ProcessedAtUtc);
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages");
            b.HasKey(x => x.Id);

            b.Property(x => x.MessageId).HasMaxLength(200).IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
            b.Property(x => x.Type).HasMaxLength(500).IsRequired();
            b.Property(x => x.Status).HasMaxLength(50).IsRequired();

            b.HasIndex(x => new { x.MessageId, x.Type }).IsUnique();         // dedupe forte
            b.HasIndex(x => new { x.CorrelationId, x.Type });                // útil pra debug
            b.HasIndex(x => x.ProcessedAtUtc);

            b.Property(x => x.Attempts).HasDefaultValue(0);
        });
    }
}