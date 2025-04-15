using Microsoft.EntityFrameworkCore;

namespace ShippingService;

public class Shipping
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ShippingAddress { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class ShippingDbContext : DbContext
{
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options) { }

    public DbSet<Shipping> Shippings => Set<Shipping>();
}