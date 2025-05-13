using Contracts;
using InventoryService;
using Microsoft.EntityFrameworkCore;
using static Contracts.Common;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IEventBus>(sp => new RabbitMQEventBus("rabbitmq.default.svc.cluster.local"));
builder.Services.AddScoped<OrderCreatedConsumer>();
builder.Services.AddScoped<InventoryCreatedConsumer>();

builder.Services.AddControllers();

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

var app = builder.Build();
var eventBus = app.Services.GetRequiredService<IEventBus>();
var consumer = app.Services.GetRequiredService<OrderCreatedConsumer>();
eventBus.Subscribe<Common.OrderCreatedEvent>(consumer);
var inventoryConsumer = app.Services.GetRequiredService<InventoryCreatedConsumer>();
eventBus.Subscribe<InventoryCreatedEvent>(inventoryConsumer);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.Migrate();
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
