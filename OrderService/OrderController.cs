using Microsoft.AspNetCore.Mvc;
using Contracts;
using static Contracts.Common;
using InventoryService;
using Microsoft.EntityFrameworkCore;
namespace OrderService;

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly InventoryDbContext _inventoryContext;
    private readonly IEventBus _eventBus;

    public OrderController(OrderDbContext context, IEventBus eventBus, InventoryDbContext inventoryContext)
    {
        _context = context;
        _eventBus = eventBus;
        _inventoryContext = inventoryContext;
    }

    [HttpGet]
    public IActionResult GetAllOrders()
    {
        var Orders = _context.Orders.ToList();
        return Ok(Orders);
    }

    [HttpPost]
    public IActionResult CreateItem([FromBody] Order order)
    {
        if (order == null)
        {
            return BadRequest("Order cannot be null");
        }

        order.CreatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(order.OrderName))
        {
            return BadRequest("Order name cannot be empty");
        }

        _context.Orders.Add(order);
        _context.SaveChanges();

        return Ok(order);
    }

    // cancel
    [HttpPost("Cancel")]
    public IActionResult CancelOrder([FromBody] Order order)
    {
        if (order == null)
        {
            return BadRequest("Cannot cancel: order cannot be null");
        }

        var existingOrder = _context.Orders.FirstOrDefault(i => i.SagaId == order.SagaId);
        if (existingOrder == null)
        {
            return NotFound("Cannot cancel: order not found");
        }

        existingOrder.IsCompensated = true;
        existingOrder.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();

        return Ok(existingOrder);
    }
    // ********************
    // CHOREOGRAPHY SAGA
    // ********************

    [HttpPost("choreography")]
    public async Task<IActionResult> CreateOrderChoreography([FromBody] Order order)
    {
        var randomSagaId = Guid.NewGuid().ToString("N");
        if (order == null)
        {
            return BadRequest("Order cannot be null");
        }

        order.CreatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        order.SagaId = randomSagaId;
        if (string.IsNullOrEmpty(order.OrderName))
        {
            return BadRequest("Order name cannot be empty");
        }

        _context.Orders.Add(order);
        _context.SaveChanges();

        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            SagaId = randomSagaId,
            Timestamp = DateTime.UtcNow
        };

        await _eventBus.PublishAsync(orderCreatedEvent);

        return Accepted(new
        {
            order.SagaId,
            status = "Started"
        });

    }

    // list all orders
    [HttpGet("list-orders")]
    public async Task<IActionResult> GetAllOrdersAsync()
    {
        var orders = await _context.Orders.ToListAsync();
        return Ok(orders);
    }

    // list all items
    [HttpGet("list-items")]
    public async Task<IActionResult> GetAllItemsAsync()
    {
        var items = await _inventoryContext.Items.ToListAsync();
        return Ok(items);
    }

    [HttpGet("saga-consistency")]
    public async Task<IActionResult> GetSagaConsistency()
    {
        // Retrieve orders and inventory items
        var orders = await _context.Orders.ToListAsync();
        var items = await _inventoryContext.Items.ToListAsync();

        // Join on SagaId and calculate metrics per saga.
        // var sagaData = (from order in orders
        //                 join item in items
        //                 on order.SagaId equals item.SagaId
        //                 select new
        //                 {
        //                     order.SagaId,
        //                     OrderCompensated = order.IsCompensated,
        //                     InventoryCompensated = item.IsCompensated,
        //                     Inconsistent = (order.IsCompensated != item.IsCompensated),
        //                     MinTime = order.CreatedAt < item.CreatedAt ? order.CreatedAt : item.CreatedAt,
        //                     MaxTime = order.UpdatedAt > item.UpdatedAt ? order.UpdatedAt : item.UpdatedAt,
        //                     ResponseTimeMilliseconds = ((order.UpdatedAt > item.UpdatedAt ? order.UpdatedAt : item.UpdatedAt)
        //                                                   - (order.CreatedAt < item.CreatedAt ? order.CreatedAt : item.CreatedAt))
        //                                                   .TotalMilliseconds
        //                 }).ToList();
        // simply collect the order + corresponding item
        // lets have a list of tuples
        var sagaData = new List<(Order, Item?)>();

        foreach (var order in orders)
        {
            var item = items.FirstOrDefault(i => i.SagaId == order.SagaId);
            sagaData.Add((order, item));
        }


        // Aggregate metrics
        // var totalSagas = sagaData.Count;
        // var averageResponseTime = totalSagas > 0 ? sagaData.Average(s => s.ResponseTimeMilliseconds) : 0;
        // var inconsistentCount = sagaData.Count(s => s.Inconsistent);

        // You can merge these with any external metrics from your k6 tool if required.
        // For example, you might query an endpoint exporting k6 metrics and append it to the payload.
        // Here we simply include our computed metrics.
        // var result = new
        // {
        //     SagaDetails = sagaData,
        //     Metrics = new
        //     {
        //         TotalSagas = totalSagas,
        //         InconsistentSagas = inconsistentCount,
        //         AverageResponseTimeMs = averageResponseTime
        //     }
        // };

        // we need to collect the p95, p90
        // checks total (order and item tuples)
        // check succeeded in 100%
        // checks failed = inconsistentCount

        // collect the avg ms, min ms, med ms, max ms

        var responseTimesPerSagaId = new Dictionary<string, List<double>>();

        foreach (var (order, item) in sagaData)
        {
            if (item != null)
            {
                // var minTime is the smallest between 4 values
                var minTime = new[] { order.CreatedAt, order.UpdatedAt, item.CreatedAt, item.UpdatedAt }.Min();
                var maxTime = new[] { order.CreatedAt, order.UpdatedAt, item.CreatedAt, item.UpdatedAt }.Max();
                var responseTime = (maxTime - minTime).TotalMilliseconds;

                if (!responseTimesPerSagaId.ContainsKey(order.SagaId))
                {
                    responseTimesPerSagaId[order.SagaId] = new List<double>();
                }

                responseTimesPerSagaId[order.SagaId].Add(responseTime);
            }
        }


        var allResponseTimes = responseTimesPerSagaId.Values.SelectMany(x => x).ToList();
        allResponseTimes.Sort();

        // Calculate basic metrics
        double avg = allResponseTimes.Any() ? allResponseTimes.Average() : 0;
        double min = allResponseTimes.Any() ? allResponseTimes.First() : 0;
        double max = allResponseTimes.Any() ? allResponseTimes.Last() : 0;
        double median = allResponseTimes.Any() ? ComputeMedian(allResponseTimes) : 0;
        double p90 = allResponseTimes.Any() ? ComputePercentile(allResponseTimes, 0.90) : 0;
        double p95 = allResponseTimes.Any() ? ComputePercentile(allResponseTimes, 0.95) : 0;

        var result = new
        {
            SagaDetails = sagaData.Select(s => new
            {
                SagaId = s.Item1.SagaId,
                OrderCompensated = s.Item1.IsCompensated,
                InventoryCompensated = s.Item2?.IsCompensated ?? false,
                ResponseTimeMs = s.Item2 != null ?
                    (new[] { s.Item1.CreatedAt, s.Item1.UpdatedAt, s.Item2.CreatedAt, s.Item2.UpdatedAt }.Max() -
                     new[] { s.Item1.CreatedAt, s.Item1.UpdatedAt, s.Item2.CreatedAt, s.Item2.UpdatedAt }.Min()).TotalMilliseconds : 0,
                Inconsistent = (s.Item2 != null && s.Item1.IsCompensated != s.Item2.IsCompensated)
            }),
            Metrics = new
            {
                TotalSagas = responseTimesPerSagaId.Count,
                AverageResponseTimeMs = avg,
                MinResponseTimeMs = min,
                MedianResponseTimeMs = median,
                P90ResponseTimeMs = p90,
                P95ResponseTimeMs = p95,
                MaxResponseTimeMs = max,
                InconsistentSagas = sagaData.Count(s => s.Item2 != null && s.Item1.IsCompensated != s.Item2.IsCompensated)
            }
        };

        return Ok(result);
    }

    private double ComputePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues == null || sortedValues.Count == 0)
            return 0;

        int N = sortedValues.Count;
        double n = (N - 1) * percentile + 1;
        if (n == 1d) return sortedValues[0];
        else if (n == N) return sortedValues[N - 1];
        else
        {
            int k = (int)n;
            double d = n - k;
            return sortedValues[k - 1] + d * (sortedValues[k] - sortedValues[k - 1]);
        }
    }

    private double ComputeMedian(List<double> sortedValues)
    {
        int count = sortedValues.Count;
        if (count % 2 == 0)
        {
            return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2;
        }
        else
        {
            return sortedValues[count / 2];
        }
    }
}