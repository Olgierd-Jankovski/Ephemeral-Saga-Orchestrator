// Program.cs

namespace ESO;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
public static class Program
{
    public static async Task Main(string[] args)
    {

        var builder = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddDbContext<EsoDbContext>(opt =>
                            opt.UseNpgsql(ctx.Configuration.GetConnectionString("EsoDb")));
                        services.AddTransient<EsoWorker>();
                    });

        var host = builder.Build();

        // Apply pending migrations.
        using (var migrationScope = host.Services.CreateScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<EsoDbContext>();
            db.Database.Migrate();
        }

        var sagaId = args.Length > 0 ? args[0] : Guid.NewGuid().ToString();
        Console.WriteLine($"[ESO] Starting with sagaId={sagaId}");

        using var scope = host.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<EsoWorker>();
        await orchestrator.RunSagaAsync(sagaId);

        Console.WriteLine("[ESO] Exiting ephemeral orchestrator.");
    }
}
