using Microsoft.AspNetCore.Mvc;

namespace InventoryService;

[ApiController]
[Route("[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _context;

    public InventoryController(InventoryDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetAllItems()
    {
        var items = _context.Items.ToList();
        return Ok(items);
    }

    [HttpPost]
    public IActionResult CreateItem([FromBody] Item item)
    {
        if (item == null)
        {
            return BadRequest("Item cannot be null");
        }

        item.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(item.ItemName))
        {
            return BadRequest("Item name cannot be empty");
        }

        _context.Items.Add(item);
        _context.SaveChanges();

        return Ok(item);
    }
}