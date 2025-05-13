using ESO;
using GatewayService.Infrastructure;
using k8s;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RecoveryService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly TimeSpan _period = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKubernetes _k8s;
    private readonly IJobLauncher _launcher;

    public Worker(ILogger<Worker> log,
                      IServiceScopeFactory scopeFactory,
                      IKubernetes k8s,
                      IJobLauncher launcher)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _k8s = k8s;
        _launcher = launcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("RecoveryService start");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndRecoverAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Scan failed");
            }

            await Task.Delay(_period, stoppingToken);
        }
    }

    private async Task ScanAndRecoverAsync(CancellationToken ct)
    {

        using var scope = _scopeFactory.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<ESO.EsoDbContext>();

        // var inFlight = await _db.SagaStates
        //                         .Where(s => s.Status == "IN_PROGRESS")
        //                         .Select(s => s.SagaId)
        //                         .Distinct()
        //                         .ToListAsync(ct);
        // select distinct saga ids, sorted by createdAt
        var inFlight = await _db.SagaStates
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s)
            .Distinct()
            .ToListAsync(ct);

        foreach (var saga in inFlight)
        {
            // if the saga is completed - lets delete all sagas by this sagaId
            if(saga.Status is "DONE")
            {
                _log.LogInformation("Saga {saga} is completed, deleting all related sagas", saga.SagaId);
                var relatedSagas = await _db.SagaStates
                    .Where(s => s.SagaId == saga.SagaId)
                    .ToListAsync(ct);

                foreach (var relatedSaga in relatedSagas)
                {
                    _db.SagaStates.Remove(relatedSaga);
                }
                await _db.SaveChangesAsync(ct);
                continue;
            }
            
            var jobs = await _k8s.BatchV1.ListNamespacedJobAsync("default",
                        labelSelector: $"sagaId={saga.SagaId}", cancellationToken: ct);

            bool active = jobs.Items.Any(j => j.Status?.Active > 0);

            if (!active)
            {
                _log.LogWarning("Saga {saga} be aktyvaus ESO – paleidžiam naują job‘ą", saga.SagaId);
                await _launcher.LaunchSagaJobAsync(saga.SagaId, ct);
            }
        }
    }
}
