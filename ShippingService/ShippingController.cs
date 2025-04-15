using Microsoft.AspNetCore.Mvc;

namespace ShippingService;

[ApiController]
[Route("[controller]")]
public class ShippingController : ControllerBase
{
    private readonly ShippingDbContext _context;

    public ShippingController(ShippingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetAllShippings()
    {
        var Shippings = _context.Shippings.ToList();
        return Ok(Shippings);
    }

    [HttpPost]
    public IActionResult CreateItem([FromBody] Shipping shipping)
    {
        if (shipping == null)
        {
            return BadRequest("Shipping cannot be null");
        }

        shipping.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(shipping.ShippingAddress))
        {
            return BadRequest("Shipping address cannot be empty");
        }

        _context.Shippings.Add(shipping);
        _context.SaveChanges();

        return Ok(shipping);
    }
}