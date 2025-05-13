using Contracts;
using InventoryService;
using Microsoft.EntityFrameworkCore;
using OrderService;
using static Contracts.Common;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<InventoryCancelledConsumer>();


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// add another db context- the inventoryt one
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryDefaultConnection")));


builder.Services.AddSingleton<IEventBus>(sp => new RabbitMQEventBus("rabbitmq.default.svc.cluster.local"));


var app = builder.Build();

var orderEventBus = app.Services.GetRequiredService<IEventBus>();
var invCancelledConsumer = app.Services.GetRequiredService<InventoryCancelledConsumer>();
orderEventBus.Subscribe<Common.InventoryCancelledEvent>(invCancelledConsumer);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureDeleted();
    db.Database.Migrate();

    var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    inventoryDb.Database.EnsureDeleted();
    inventoryDb.Database.Migrate();
}


// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseHttpsRedirection();
app.MapControllers();

app.Run();