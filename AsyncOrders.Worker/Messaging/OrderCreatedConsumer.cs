using System.Text;
using System.Text.Json;
using AsyncOrders.Application.Abstractions.Messaging;
using AsyncOrders.Application.Abstractions.Persistence;
using AsyncOrders.Application.Orders.Events;
using AsyncOrders.Domain.Orders;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AsyncOrders.Worker.Messaging;

public sealed class OrderCreatedConsumer : BackgroundService
{
    private const int MaxAttempts = 5;

    private const string MainQueue = "orders.created.q";

    private const string Exchange = "orders.ex";
    private const string RoutingKey = "orders.created";
    private const string DlqRoutingKey = "orders.created.dlq";

    private const string DlqQueue = "orders.created.dlq.q";

    // Delay queues (TTL por fila)
    private const string Delay5s = "orders.created.delay.5s.q";
    private const string Delay15s = "orders.created.delay.15s.q";
    private const string Delay30s = "orders.created.delay.30s.q";
    private const string Delay60s = "orders.created.delay.60s.q";

    private const string MessageType = nameof(OrderCreatedEvent);

    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;

    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public OrderCreatedConsumer(IServiceProvider sp, IConfiguration cfg)
    {
        _sp = sp;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Config com fallback (evita crash por config faltante)
        var host = _cfg["RabbitMq:Host"] ?? "localhost";
        var port = int.TryParse(_cfg["RabbitMq:Port"], out var p) ? p : 5672;
        var user = _cfg["RabbitMq:User"] ?? "guest";
        var pass = _cfg["RabbitMq:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = pass
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // ================
        // Topologia
        // ================
        await EnsureTopologyAsync(_channel, stoppingToken);

        // 1 mensagem por vez (simples e seguro no começo)
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var ch = _channel!;
            var ct = stoppingToken;

            // CorrelationId (preferencialmente do header; fallback para o evento depois)
            var correlationId =
                TryGetHeaderString(ea.BasicProperties?.Headers, "x-correlation-id")
                ?? string.Empty;

            // MessageId (Inbox precisa de uma chave estável; usa o do broker se tiver)
            var messageId = ea.BasicProperties?.MessageId;
            if (string.IsNullOrWhiteSpace(messageId))
            {
                // fallback determinístico o suficiente para dedupe local
                messageId = $"{correlationId}:{ea.DeliveryTag}";
            }

            // Parse do evento
            OrderCreatedEvent? evt;
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                evt = JsonSerializer.Deserialize<OrderCreatedEvent>(json, JsonOpts);
            }
            catch
            {
                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                return;
            }

            if (evt is null)
            {
                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                return;
            }

            // Se não veio correlationId no header, pega do evento
            if (string.IsNullOrWhiteSpace(correlationId))
                correlationId = evt.CorrelationId;

            using var scope = _sp.CreateScope();

            var inbox = scope.ServiceProvider.GetRequiredService<IInboxStore>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Inbox: se já processou, ACK e sai (idempotência por entrega)
            if (await inbox.HasProcessedAsync(messageId!, MessageType, ct))
            {
                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                return;
            }

            // Marca início do processamento (não segura transação longa)
            await inbox.StartProcessingAsync(messageId!, correlationId, MessageType, DateTime.UtcNow, ct);
            await uow.SaveChangesAsync(ct);

            var orders = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var logs = scope.ServiceProvider.GetRequiredService<IOrderProcessingLogRepository>();

            // Idempotência de domínio (se já processou com sucesso pelo correlationId, finaliza inbox e ACK)
            if (await logs.HasSucceededForCorrelationIdAsync(evt.CorrelationId, ct))
            {
                await inbox.MarkCompletedAsync(messageId!, MessageType, DateTime.UtcNow, ct);
                await uow.SaveChangesAsync(ct);

                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                return;
            }

            var order = await orders.GetByIdAsync(evt.OrderId, ct);
            if (order is null)
            {
                await inbox.MarkFailedAsync(messageId!, MessageType, "Order not found", DateTime.UtcNow, ct);
                await uow.SaveChangesAsync(ct);

                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                return;
            }

            var startedAt = DateTime.UtcNow;

            try
            {
                // Pending -> Processing
                order.MarkProcessing(DateTime.UtcNow);
                await orders.UpdateAsync(order, ct);

                // Simula trabalho (pode remover depois)
                await Task.Delay(1500, ct);

                // Processing -> Completed
                order.MarkCompleted(DateTime.UtcNow);
                await orders.UpdateAsync(order, ct);

                var successLog = new OrderProcessingLog(
                    id: Guid.NewGuid(),
                    orderId: evt.OrderId,
                    correlationId: evt.CorrelationId,
                    attempt: 1,
                    startedAtUtc: startedAt
                );
                successLog.MarkSuccess(DateTime.UtcNow);

                await logs.AddAsync(successLog, ct);

                // Fecha inbox como concluído
                await inbox.MarkCompletedAsync(messageId!, MessageType, DateTime.UtcNow, ct);

                await uow.SaveChangesAsync(ct);

                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                // Best-effort: marca falha no pedido e grava log de falha
                try
                {
                    order.MarkFailed(ex.Message, DateTime.UtcNow);
                    await orders.UpdateAsync(order, ct);

                    var failureLog = new OrderProcessingLog(
                        id: Guid.NewGuid(),
                        orderId: evt.OrderId,
                        correlationId: evt.CorrelationId,
                        attempt: 1,
                        startedAtUtc: startedAt
                    );
                    failureLog.MarkFailure(ex.Message, DateTime.UtcNow);

                    await logs.AddAsync(failureLog, ct);

                    await inbox.MarkFailedAsync(messageId!, MessageType, ex.Message, DateTime.UtcNow, ct);

                    await uow.SaveChangesAsync(ct);
                }
                catch
                {
                    // não derruba o worker
                }

                // Retry / DLQ com Delay Queue (sem Task.Delay)
                var attempt = GetAttempt(ea.BasicProperties?.Headers);
                var nextAttempt = attempt + 1;

                var newHeaders = BuildHeaders(ea.BasicProperties?.Headers, correlationId, nextAttempt);

                var props = new BasicProperties
                {
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    MessageId = messageId, // ajuda na dedupe via Inbox
                    Headers = newHeaders
                };

                if (nextAttempt <= MaxAttempts)
                {
                    var delayQueue = GetDelayQueueName(nextAttempt);

                    // Publica NO DEFAULT EXCHANGE ("") com routingKey = nome da fila delay.
                    // Após TTL, Rabbit envia de volta pra Exchange+RoutingKey via DLX.
                    await ch.BasicPublishAsync(
                        exchange: "",
                        routingKey: delayQueue,
                        mandatory: false,
                        basicProperties: props,
                        body: ea.Body,
                        cancellationToken: ct);
                }
                else
                {
                    await ch.BasicPublishAsync(
                        exchange: Exchange,
                        routingKey: DlqRoutingKey,
                        mandatory: false,
                        basicProperties: props,
                        body: ea.Body,
                        cancellationToken: ct);
                }

                // ACK da original só depois de publicar retry/DLQ
                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: MainQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static async Task EnsureTopologyAsync(IChannel ch, CancellationToken ct)
    {
        // Exchange principal
        await ch.ExchangeDeclareAsync(
            exchange: Exchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        // Main queue
        await ch.QueueDeclareAsync(
            queue: MainQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: MainQueue,
            exchange: Exchange,
            routingKey: RoutingKey,
            arguments: null,
            cancellationToken: ct);

        // Delay queues (cada uma com TTL e DLX de volta pro routing principal)
        await DeclareDelayQueueAsync(ch, Delay5s, 5000, ct);
        await DeclareDelayQueueAsync(ch, Delay15s, 15000, ct);
        await DeclareDelayQueueAsync(ch, Delay30s, 30000, ct);
        await DeclareDelayQueueAsync(ch, Delay60s, 60000, ct);

        // DLQ final
        await ch.QueueDeclareAsync(
            queue: DlqQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: DlqQueue,
            exchange: Exchange,
            routingKey: DlqRoutingKey,
            arguments: null,
            cancellationToken: ct);
    }

    private static Task DeclareDelayQueueAsync(IChannel ch, string queue, int ttlMs, CancellationToken ct)
    {
        var args = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = ttlMs,
            ["x-dead-letter-exchange"] = Exchange,
            ["x-dead-letter-routing-key"] = RoutingKey
        };

        return ch.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args,
            cancellationToken: ct);
    }

    private static string GetDelayQueueName(int nextAttempt) => nextAttempt switch
    {
        2 => Delay5s,
        3 => Delay15s,
        4 => Delay30s,
        _ => Delay60s
    };

    private static int GetAttempt(IDictionary<string, object?>? headers)
    {
        if (headers is null) return 1;
        if (!headers.TryGetValue("x-attempt", out var raw) || raw is null) return 1;

        return raw switch
        {
            byte b => b,
            sbyte sb => sb,
            short s => s,
            int i => i,
            long l => (int)l,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var v) => v,
            _ => 1
        };
    }

    private static IDictionary<string, object?> BuildHeaders(IDictionary<string, object?>? existing, string correlationId, int nextAttempt)
    {
        var h = new Dictionary<string, object?>();

        if (existing is not null)
        {
            foreach (var kv in existing)
                h[kv.Key] = kv.Value;
        }

        h["x-correlation-id"] = correlationId;
        h["x-attempt"] = nextAttempt;

        return h;
    }

    private static string? TryGetHeaderString(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null) return null;
        if (!headers.TryGetValue(key, out var raw) || raw is null) return null;

        return raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            _ => raw.ToString()
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);

        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
