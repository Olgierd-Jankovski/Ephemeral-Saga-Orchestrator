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

        // 2) Step "AuthorizePayment"
        bool step2Ok = await ExecuteStepAsync(
            sagaId, "AuthorizePayment",
            () => AuthorizePayment(sagaId));
        if (!step2Ok)
        {
            Console.WriteLine("[ESO] Payment step failed => compensating previous step(s)...");
            await CompensateAllAsync(sagaId);
            return;
        }

        // 3) Step "PrepareShipping"
        bool step3Ok = await ExecuteStepAsync(
            sagaId, "PrepareShipping",
            () => PrepareShipping(sagaId));
        if (!step3Ok)
        {
            Console.WriteLine("[ESO] Shipping failed => undo Payment & Order");
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

        if (existingStep != null && existingStep.Status == "Success")
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
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _db.SagaStates.Add(existingStep);
        }
        else
        {
            existingStep.Status = "Pending";
            existingStep.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        // 3) Tikras kvietimas
        var success = await action();
        if (success)
        {
            existingStep.Status = "Success";
            existingStep.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }
        else
        {
            existingStep.Status = "Failed";
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

    private async Task<bool> AuthorizePayment(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://payment-service/Payment",
                new StringContent($"{{\"sagaId\": \"{sagaId}\", \"amount\": 999}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ESO] AuthorizePayment ex => " + ex.Message);
            return false;
        }
    }

    private async Task<bool> PrepareShipping(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://shipping-service/Shipping",
                new StringContent($"{{\"sagaId\": \"{sagaId}\"}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ESO] PrepareShipping ex => " + ex.Message);
            return false;
        }
    }

    // ====== Kompensacija ======
    private async Task CompensateAllAsync(string sagaId)
    {
        return;
        // Surandame step'us, kurie Success
        var successSteps = await _db.SagaStates
            .Where(x => x.SagaId == sagaId && x.Status == "Success")
            .OrderByDescending(x => x.Id)  // kad kompensuotume atvirkštine tvarka
            .ToListAsync();

        foreach (var st in successSteps)
        {
            Console.WriteLine($"[ESO] Compensating step {st.StepName}");
            switch (st.StepName)
            {
                case "PrepareShipping":
                    await CancelShipping(sagaId);
                    break;
                case "AuthorizePayment":
                    await CancelPayment(sagaId);
                    break;
                case "CreateOrder":
                    await CancelOrder(sagaId);
                    break;
            }
            st.Status = "Compensated";
            st.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private async Task<bool> CancelShipping(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://shipping-service/shipping/cancel",
                new StringContent($"{{\"sagaId\": \"{sagaId}\"}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<bool> CancelPayment(string sagaId)
    {
        try
        {
            var response = await _httpClient.PostAsync("http://payment-service/payment/cancel",
                new StringContent($"{{\"sagaId\": \"{sagaId}\"}}", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
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
}
