using AsyncOrders.Api.Messaging;
using AsyncOrders.Application.Abstractions.Messaging;
using AsyncOrders.Application.Abstractions.Persistence;
using AsyncOrders.Application.Abstractions.Time;
using AsyncOrders.Application.Orders.Commands.CreateOrder;
using AsyncOrders.Infrastructure.Messaging.RabbitMq;
using AsyncOrders.Infrastructure.Persistence;
using AsyncOrders.Infrastructure.Persistence.Inbox;
using AsyncOrders.Infrastructure.Persistence.Outbox;
using AsyncOrders.Infrastructure.Persistence.Repositories;
using AsyncOrders.Infrastructure.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var cs = builder.Configuration.GetConnectionString("Default")
         ?? throw new InvalidOperationException("Connection string 'Default' not found.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(cs, sql => sql.EnableRetryOnFailure()));

// Persistence
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IOutboxWriter, EfOutboxWriter>();
builder.Services.AddScoped<IInboxStore, EfInboxStore>();

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderProcessingLogRepository, OrderProcessingLogRepository>();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddScoped<CreateOrderService>();
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();

// Outbox dispatcher roda na API
builder.Services.AddHostedService<OutboxDispatcher>();

// Rabbit publisher real
var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()
             ?? throw new InvalidOperationException("RabbitMq config missing.");

builder.Services.AddSingleton(rabbit);
builder.Services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
