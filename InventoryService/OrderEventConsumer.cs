using Contracts;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using static Contracts.Common;

namespace InventoryService
{
    public class OrderCreatedConsumer : IEventConsumer<Common.OrderCreatedEvent>
    {
        private readonly ILogger<OrderCreatedConsumer> _logger;
        // db context
        private readonly IServiceScopeFactory _scopeFactory;
        // event bus
        private readonly IEventBus _eventBus;
        public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger, IServiceScopeFactory scopeFactory, IEventBus eventBus)
        {
            _logger = logger;
            _eventBus = eventBus;
            _scopeFactory = scopeFactory;
        }

        public async Task ConsumeAsync(Common.OrderCreatedEvent domainEvent)
        {
            using var scope = _scopeFactory.CreateScope();
            var _db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var item = new Item
            {
                SagaId = domainEvent.SagaId,
                ItemName = "Item for order " + domainEvent.OrderId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.Items.Add(item);
            await _db.SaveChangesAsync();
            _logger.LogInformation($"[InventoryService] OrderCreatedConsumer: Created item with ID {item.Id} for order {domainEvent.OrderId}");

            // publication of the event
            var inventoryCreatedEvent = new InventoryCreatedEvent
            {
                InventoryId = item.Id,
                SagaId = domainEvent.SagaId,
                Timestamp = DateTime.UtcNow
            };

            // publish the event
            await _eventBus.PublishAsync(inventoryCreatedEvent);

        }
    }
}