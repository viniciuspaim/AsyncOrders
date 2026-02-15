using AsyncOrders.Application.Abstractions.Messaging;
using AsyncOrders.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncOrders.Infrastructure.Persistence.Inbox;

public sealed class EfInboxStore : IInboxStore
{
    private readonly AppDbContext _db;
    public EfInboxStore(AppDbContext db) => _db = db;

    public Task<bool> HasProcessedAsync(string messageId, string type, CancellationToken ct)
        => _db.InboxMessages.AnyAsync(x => x.MessageId == messageId && x.Type == type && x.Status == "Completed", ct);

    public async Task StartProcessingAsync(string messageId, string correlationId, string type, DateTime receivedAtUtc, CancellationToken ct)
    {
        // Se já existe, não cria duplicado (evita violar Unique Index)
        var existing = await _db.InboxMessages
            .SingleOrDefaultAsync(x => x.MessageId == messageId && x.Type == type, ct);

        if (existing is not null)
        {
            existing.Attempts++;
            existing.Status = "Processing";
            existing.LastError = null;
            return;
        }

        _db.InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            CorrelationId = correlationId,
            Type = type,
            ReceivedAtUtc = receivedAtUtc,
            Status = "Processing",
            Attempts = 1
        });
    }

    public async Task MarkCompletedAsync(string messageId, string type, DateTime processedAtUtc, CancellationToken ct)
    {
        var msg = await _db.InboxMessages.SingleAsync(x => x.MessageId == messageId && x.Type == type, ct);
        msg.Status = "Completed";
        msg.ProcessedAtUtc = processedAtUtc;
        msg.LastError = null;
    }

    public async Task MarkFailedAsync(string messageId, string type, string error, DateTime failedAtUtc, CancellationToken ct)
    {
        var msg = await _db.InboxMessages.SingleAsync(x => x.MessageId == messageId && x.Type == type, ct);
        msg.Status = "Failed";
        msg.LastError = error;
        // não preenche ProcessedAtUtc
    }
}