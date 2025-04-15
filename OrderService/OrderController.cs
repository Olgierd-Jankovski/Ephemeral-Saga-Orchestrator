using Microsoft.AspNetCore.Mvc;

namespace OrderService;

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext _context;

    public OrderController(OrderDbContext context)
    {
        _context = context;
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
        if (string.IsNullOrEmpty(order.OrderName))
        {
            return BadRequest("Order name cannot be empty");
        }

        _context.Orders.Add(order);
        _context.SaveChanges();

        return Ok(order);
    }
}