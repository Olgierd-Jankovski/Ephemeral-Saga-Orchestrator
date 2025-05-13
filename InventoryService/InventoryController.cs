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
        item.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(item.ItemName))
        {
            return BadRequest("Item name cannot be empty");
        }

        _context.Items.Add(item);
        _context.SaveChanges();

        return Ok(item);
    }


    // create the cancel endpoint, it accepts the sagaId
    [HttpPost("Cancel")]
    public IActionResult CancelItem([FromBody] Item item)
    {
        if (item == null)
        {
            return BadRequest("Cannot cancel: item cannot be null");
        }

        var existingItem = _context.Items.FirstOrDefault(i => i.SagaId == item.SagaId);
        if (existingItem == null)
        {
            return NotFound("Cannot cancel: item not found");
        }

        existingItem.IsCompensated = true;
        existingItem.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();

        return Ok(existingItem);
    }
}