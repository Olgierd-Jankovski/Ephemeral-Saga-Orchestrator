using Microsoft.EntityFrameworkCore;

namespace PaymentService;

public class Payment {
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentDbContext : DbContext {
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
}