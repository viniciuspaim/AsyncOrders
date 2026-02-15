using AsyncOrders.Application.Abstractions.Messaging;
using AsyncOrders.Application.Abstractions.Persistence;
using AsyncOrders.Infrastructure.Persistence;
using AsyncOrders.Infrastructure.Persistence.Inbox;
using AsyncOrders.Infrastructure.Persistence.Repositories;
using AsyncOrders.Worker.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var cs = builder.Configuration.GetConnectionString("Default")
         ?? throw new InvalidOperationException("Connection string 'Default' not found for Worker.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(cs, sql => sql.EnableRetryOnFailure()));

// Repos
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderProcessingLogRepository, OrderProcessingLogRepository>();

// Unit of Work (necessário para Inbox)
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

// Inbox Store
builder.Services.AddScoped<IInboxStore, EfInboxStore>();

// Consumer
builder.Services.AddHostedService<OrderCreatedConsumer>();

var host = builder.Build();
host.Run();
