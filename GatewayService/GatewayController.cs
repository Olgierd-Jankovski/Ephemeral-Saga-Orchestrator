using ESO;
using GatewayService.Infrastructure;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;

namespace GatewayService.Controllers;

[ApiController]
[Route("[controller]")]
public class GatewayController : ControllerBase
{
    private readonly IJobLauncher _jobs;
    private readonly OutboxDbContext _outboxDbContext;
    // private readonly IServiceScopeFactory _scopeFactory;
    private readonly EsoWorker _esoWorker;




    // public GatewayController(IJobLauncher jobs, OutboxDbContext outboxDbContext, IServiceScopeFactory scopeFactory)
    public GatewayController(IJobLauncher jobs, OutboxDbContext outboxDbContext, IServiceScopeFactory scopeFactory, EsoWorker esoWorker)
    {
        _esoWorker = esoWorker;
        _jobs = jobs;
        _outboxDbContext = outboxDbContext;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartSaga(CancellationToken ct)
    {
        var sagaId = Guid.NewGuid().ToString("N");
        await _jobs.LaunchSagaJobAsync(sagaId, ct);

        // return AcceptedAtAction(nameof(GetSagaStatus), new { sagaId });
        // return Accepted();
        // return simply the response with teh saga id
        return Accepted(new
        {
            sagaId,
            status = "Started"
        });
    }

    // this endpoint launches the eso job without kubernetes job. It launch eso-workers internally
    [HttpPost("neso-start")]
    public async Task<IActionResult> StartNesoSaga(CancellationToken ct)
    {
        var sagaId = Guid.NewGuid().ToString("N");
        // using var scope = _scopeFactory.CreateScope();
        // var esoWorker = scope.ServiceProvider.GetRequiredService<EsoWorker>();
        // add into the db context that we started
        var outbox = new Outbox
        {
            SagaId = sagaId,
            Status = "STARTED"
        };
        _outboxDbContext.Outboxes.Add(outbox);
        await _outboxDbContext.SaveChangesAsync(ct);

        // await esoWorker.RunSagaAsync(sagaId);
        try
        {

            await _esoWorker.RunSagaAsync(sagaId);

            outbox.Status = "COMPLETED";
            _outboxDbContext.Outboxes.Update(outbox);
            await _outboxDbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // add into the db context that we failed
            outbox.Status = "FAILED";
            _outboxDbContext.Outboxes.Update(outbox);
            await _outboxDbContext.SaveChangesAsync(ct);
            // return that we accepted, relay on the outbox
            return Accepted(new
            {
                sagaId,
                status = "STARTED"
            });
        }

        return Accepted(new
        {
            sagaId,
            status = "STARTED"
        });
    }

    [HttpGet("{sagaId}")]
    public async Task<IActionResult> GetSagaStatus(string sagaId, CancellationToken ct)
    {
        V1Job? job = await _jobs.GetJobAsync(sagaId, ct);
        if (job is null) return NotFound();

        var status = job.Status;
        string state = status?.Succeeded > 0 ? "Succeeded"
                     : status?.Failed > 0 ? "Failed"
                     : "Running";

        return Ok(new
        {
            sagaId,
            state,
            startedAt = status?.StartTime,
            finishedAt = status?.CompletionTime,
            active = status?.Active
        });
    }
}
