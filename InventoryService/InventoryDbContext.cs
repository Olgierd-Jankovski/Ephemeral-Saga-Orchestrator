using Microsoft.EntityFrameworkCore;

namespace InventoryService;

public class Item
{
    public int Id { get; set; }
    public string SagaId { get; set; } = "";
    public bool IsCompensated { get; set; } = false;
    public string ItemName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
}