using GatewayService;
using GatewayService.Infrastructure;
using k8s;
using ESO;
using Microsoft.EntityFrameworkCore;
using RecoveryService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();


// register options
builder.Services.Configure<JobTemplateOptions>(
    builder.Configuration.GetSection("JobTemplate"));

// override it ONLY whenever we are in the kubernetes (test) environment
if (KubernetesClientConfiguration.IsInCluster())
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    var newConnectionString = "Host=eso-db-service;Port=5432;Database=esoDb;Username=postgres;Password=admin";
    builder.Configuration["ConnectionStrings:Default"] = newConnectionString;

    var outboxConnectionString = builder.Configuration.GetConnectionString("Outbox");
    var newOutboxConnectionString = "Host=outbox-db-service;Port=5432;Database=outboxDb;Username=postgres;Password=admin";
    builder.Configuration["ConnectionStrings:Outbox"] = newOutboxConnectionString;

    // Load YAML file and add it to cofig
    var contentRoot = builder.Environment.ContentRootPath;
    var jobTemplateYaml = File.ReadAllText(Path.Combine(contentRoot, "Properties", "jobTemplate.yaml"));
    builder.Configuration["JobTemplate:TemplateYaml"] = jobTemplateYaml;
}
else
{
    // Load YAML file and add it to cofig
    var jobTemplateYaml = File.ReadAllText("Properties/jobTemplate.yaml");
    builder.Configuration["JobTemplate:TemplateYaml"] = jobTemplateYaml;

}

builder.Services.AddControllers();

builder.Services.AddDbContext<ESO.EsoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddDbContext<GatewayService.OutboxDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Outbox")));

builder.Services.AddSingleton<IKubernetes>(_ =>
{
    var cfg = KubernetesClientConfiguration.IsInCluster()
              ? KubernetesClientConfiguration.InClusterConfig()
              : KubernetesClientConfiguration.BuildConfigFromConfigFile();
    return new Kubernetes(cfg);
});

// Job YAML šablonas per Options pattern
builder.Services.Configure<JobTemplateOptions>(
    builder.Configuration.GetSection("JobTemplate"));
builder.Services.AddSingleton<IJobLauncher, K8sJobLauncher>();

// builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<OutboxRelay>();

// used for eso-worker internal launch withing the request (no jobs, no kubernetes, plain request)
builder.Services.AddTransient<EsoWorker>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ESO.EsoDbContext>();
    // remove everything from the database
    db.Database.EnsureDeleted();
    db.Database.Migrate();

    var outboxDb = scope.ServiceProvider.GetRequiredService<GatewayService.OutboxDbContext>();
    // remove everything from the database
    outboxDb.Database.EnsureDeleted();
    outboxDb.Database.Migrate();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();