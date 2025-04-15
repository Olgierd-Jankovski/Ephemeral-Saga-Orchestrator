using Microsoft.EntityFrameworkCore;

namespace ESO;

public class SagaState
{
    public int Id { get; set; }
    public string SagaId { get; set; } = null!;
    public string StepName { get; set; } = "";
    public string Status { get; set; } = "IN_PROGRESS"; // e.g. IN_PROGRESS, DONE, FAILED
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EsoDbContext : DbContext
{
    public EsoDbContext(DbContextOptions<EsoDbContext> options) : base(options) { }

    public DbSet<SagaState> SagaStates => Set<SagaState>();
}

