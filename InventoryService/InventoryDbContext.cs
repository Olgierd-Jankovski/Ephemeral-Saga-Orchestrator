using Microsoft.EntityFrameworkCore;

namespace InventoryService;

public class Item
{
    public int Id { get; set; }
    public string ItemName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
}