using Microsoft.EntityFrameworkCore;

namespace GatewayService;

// CREATE THE DB CONTEXT, IT WILL STORE SAGAS TO TRACK REQUESTS, IT CAN HAVE THEIR STATUSES
// RUNNING, FAILED, COMPLETED

public class Outbox
{
    public int Id { get; set; }
    public string SagaId { get; set; } = "";
    public string Status { get; set; } = "";
}

public class OutboxDbContext : DbContext
{
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options) { }

    public DbSet<Outbox> Outboxes => Set<Outbox>();
}
