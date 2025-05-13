namespace ESO;

using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;

public class EsoWorker
{
    private readonly HttpClient _httpClient;
    private readonly EsoDbContext _db;

    public EsoWorker(EsoDbContext db)
    {
        _httpClient = new HttpClient();
        _db = db;
    }

    public async Task RunSagaAsync(string sagaId)
    {
        Console.WriteLine($"[ESO] Starting saga: {sagaId}");

        // 1) Step "CreateOrder"
        bool step1Ok = await ExecuteStepAsync(
            sagaId, "CreateOrder",
            () => CreateOrder(sagaId));
        if (!step1Ok)
        {
            Console.WriteLine("[ESO] Step CreateOrder failed => no previous step to compensate? (or do partial compensation). Exiting.");
            return;
        }

        if (new Random().Next(1, 10) == 1)
        {
            // exception simulation, job scheduled, we throw
            Console.WriteLine("[ESO] Simulating exception in CreateInventory step.");
            throw new Exception("Simulated exception in CreateInventory step.");
        }

        // 2) Step "CreateInventory"
        bool step2Ok = await ExecuteStepAsync(
            sagaId, "CreateInventory",
            () => CreateInventory(sagaId));
        if (!step2Ok)
        {
            Console.WriteLine("[ESO] Inventory step failed => compensating previous step(s)...");
            await CompensateAllAsync(sagaId);
            return;
        }

        Console.WriteLine($"[ESO] Saga {sagaId} completed successfully!");

    }

    private async Task<bool> ExecuteStepAsync(string sagaId, string stepName, Func<Task<bool>> action)
    {
        // 1) Patikriname, ar stepName jau exist ir turi Status=Success => skip
        var existingStep = await _db.SagaStates
            .FirstOrDefaultAsync(x => x.SagaId == sagaId && x.StepName == stepName);

        if (existingStep != null && existingStep.Status == "DONE")
        {
            Console.WriteLine($"[ESO] Step '{stepName}' already succeeded. Skipping.");
            return true;
        }

        // 2) Sukuriame ar atnaujiname Step => Pending
        if (existingStep == null)
        {
            existingStep = new SagaState
            {
                SagaId = sagaId,
                StepName = stepName,
                Status = "IN_PROGRESS",
                CreatedAt = DateTime.UtcNow
            };
            _db.SagaStates.Add(existingStep);
        }
        else
        {
            existingStep.Status = "IN_PROGRESS";
            existingStep.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        // 3) Tikras kvietimas
        var success = await action();
        if (success)
        {
            existingStep.Status = "DONE";
            existingStep.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }
        else
        {
            existingStep.Status = "FAILED";
            existingStep.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return false;
        }
    }

    // ====== Realūs kvietimai į kitas paslaugas ======
    private async Task<bool> CreateOrder(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://order-service/Order",
                new StringContent($"{{\"sagaId\": \"{sagaId}\", \"orderName\":\"Person buys a laptop\"}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ESO] CreateOrder ex => " + ex.Message);
            return false;
        }
    }

    private async Task<bool> CreateInventory(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://inventory-service/Inventory",
                new StringContent($"{{\"sagaId\": \"{sagaId}\", \"itemName\": \"item name\"}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ESO] CreateInventory ex => " + ex.Message);
            return false;
        }
    }

    // ====== Kompensacija ======
    private async Task CompensateAllAsync(string sagaId)
    {
        // Surandame step'us, kurie Success
        var successSteps = await _db.SagaStates
            .Where(x => x.SagaId == sagaId && x.Status == "DONE")
            .OrderByDescending(x => x.Id)  // kad kompensuotume atvirkštine tvarka
            .ToListAsync();

        foreach (var st in successSteps)
        {
            Console.WriteLine($"[ESO] Compensating step {st.StepName}");
            switch (st.StepName)
            {
                case "CreateOrder":
                    await CancelOrder(sagaId);
                    break;
                case "CreateInventory":
                    await CancelInventory(sagaId);
                    break;
            }
            st.Status = "DONE";
            st.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private async Task<bool> CancelOrder(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://order-service/order/cancel",
                new StringContent($"{{\"sagaId\": \"{sagaId}\"}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<bool> CancelInventory(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://inventory-service/inventory/cancel",
                new StringContent($"{{\"sagaId\": \"{sagaId}\"}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
