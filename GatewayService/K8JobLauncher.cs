using GatewayService;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GatewayService.Infrastructure;

public interface IJobLauncher
{
    Task LaunchSagaJobAsync(string sagaId, CancellationToken ct = default);
    Task<V1Job?> GetJobAsync(string sagaId, CancellationToken ct = default);
}

public sealed class K8sJobLauncher : IJobLauncher
{
    private readonly IKubernetes _k8s;
    private readonly string _template;

    private readonly IDeserializer _deserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

    public K8sJobLauncher(IKubernetes k8s, IOptions<JobTemplateOptions> opts)
    {
        _k8s = k8s;
        _template = opts.Value.TemplateYaml ?? throw new InvalidOperationException("Job template missing");
    }

    public async Task LaunchSagaJobAsync(string sagaId, CancellationToken ct = default)
    {
        // log the template
        // Console.WriteLine($"[ESO] Job template: {_template}");
        var yaml = string.Format(_template, sagaId);
        // log the job
        Console.WriteLine($"[ESO] Job YAML: {yaml}");
        var job = KubernetesYaml.Deserialize<V1Job>(yaml);
        Console.WriteLine($"[ESO] Job: {job}");

        await _k8s.BatchV1.CreateNamespacedJobAsync(job, "default", cancellationToken: ct);
    }

    public async Task<V1Job?> GetJobAsync(string sagaId, CancellationToken ct = default)
    {
        var jobs = await _k8s.BatchV1.ListNamespacedJobAsync("default",
                    labelSelector: $"sagaId={sagaId}", cancellationToken: ct);
        return jobs.Items.FirstOrDefault();
    }
}
