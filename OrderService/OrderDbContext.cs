using Microsoft.EntityFrameworkCore;

namespace OrderService;

public class Order {
    public int Id { get; set; }
    public string OrderName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class OrderDbContext : DbContext {
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
}