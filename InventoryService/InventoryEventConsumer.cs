using Contracts;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Contracts.Common;

namespace InventoryService
{
    // Consumer that processes InventoryCreatedEvent with random failure.
    public class InventoryCreatedConsumer : IEventConsumer<InventoryCreatedEvent>
    {
        private readonly InventoryDbContext _db;
        private readonly IEventBus _eventBus;
        private readonly ILogger<InventoryCreatedConsumer> _logger;

        public InventoryCreatedConsumer(InventoryDbContext db, IEventBus eventBus, ILogger<InventoryCreatedConsumer> logger)
        {
            _db = db;
            _eventBus = eventBus;
            _logger = logger;
        }

        public async Task ConsumeAsync(InventoryCreatedEvent domainEvent)
        {
            // If no exception, update the record to reflect a rolled-back inventory.
            var item = _db.Items.FirstOrDefault(i => i.SagaId == domainEvent.SagaId);
            if (item != null)
            {
                item.IsCompensated = true;
                item.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Updated inventory record for saga {sagaId} as compensated", domainEvent.SagaId);

                // Simulate random failure.
                var rnd = new Random();
                var chance = rnd.Next(0, 20); // e.g. 0-20, so ~10% failure rate if chance < 2.
                if (chance < 2)
                {
                    _logger.LogError("Simulated failure processing InventoryCreatedEvent for saga {sagaId}", domainEvent.SagaId);
                    throw new Exception("Simulated inventory processing failure");
                }

            }

            // Publish InventoryCancelledEvent so that OrderService can update its store.
            var invCancelled = new InventoryCancelledEvent
            {
                InventoryId = domainEvent.InventoryId,
                SagaId = domainEvent.SagaId,
                Timestamp = DateTime.UtcNow
            };

            await _eventBus.PublishAsync(invCancelled);
        }
    }
}