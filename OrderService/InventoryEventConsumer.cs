using Contracts;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using static Contracts.Common;

namespace OrderService
{
    // Consumer for InventoryCancelledEvent to update Order status.
    public class InventoryCancelledConsumer : IEventConsumer<InventoryCancelledEvent>
    {
        private readonly OrderDbContext _db;
        private readonly ILogger<InventoryCancelledConsumer> _logger;

        public InventoryCancelledConsumer(OrderDbContext db, ILogger<InventoryCancelledConsumer> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task ConsumeAsync(InventoryCancelledEvent domainEvent)
        {
            // Find the order with matching saga id and mark it as compensated.
            var order = _db.Orders.FirstOrDefault(o => o.SagaId == domainEvent.SagaId);
            if(order != null)
            {
                order.IsCompensated = true;
                order.UpdatedAt = DateTime.UtcNow;
                _db.Orders.Update(order);
                _db.SaveChanges();
                _logger.LogInformation("Order {orderId} compensated due to inventory cancellation", order.Id);
            }
            await Task.CompletedTask;
        }
    }
}