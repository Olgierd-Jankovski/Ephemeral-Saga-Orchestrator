
using ESO;
using Microsoft.EntityFrameworkCore;

namespace GatewayService;

public class OutboxRelay : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelay> _logger;
    private readonly TimeSpan _delay = TimeSpan.FromSeconds(1);

    public OutboxRelay(IServiceScopeFactory scopeFactory, ILogger<OutboxRelay> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var outboxDbContext = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
            var esoWorker = scope.ServiceProvider.GetRequiredService<EsoWorker>();

            var outboxes = await outboxDbContext.Outboxes
                .Where(o => o.Status == "FAILED")
                .ToListAsync(stoppingToken);

            foreach (var outbox in outboxes)
            {
                outbox.Status = "STARTED";
                outboxDbContext.Outboxes.Update(outbox);
                await outboxDbContext.SaveChangesAsync(stoppingToken);

                try
                {
                    await esoWorker.RunSagaAsync(outbox.SagaId);
                    outbox.Status = "COMPLETED";
                }
                catch (Exception ex)
                {
                    // relay will retry
                    outbox.Status = "FAILED";
                    _logger.LogError(ex, "Failed to run saga for {SagaId}", outbox.SagaId);
                }

                outboxDbContext.Outboxes.Update(outbox);
                await outboxDbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Processed outbox with SagaId: {SagaId}, Status: {Status}", outbox.SagaId, outbox.Status);
            }

            await Task.Delay(_delay, stoppingToken);
        }
    }
}
