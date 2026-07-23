using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Contracts.Modules;

namespace SharpClaw.ModuleHost.OutOfProcess.Tests;

public sealed class LifecycleSmokeModule : ISharpClawCoreModule
{
    public string Id => "lifecycle_smoke_module";
    public string DisplayName => "Lifecycle Smoke";
    public string ToolPrefix => "smoke";

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public IReadOnlyList<ModuleToolDefinition> GetToolDefinitions() => [];

    public Task<string> ExecuteToolAsync(
        string toolName,
        JsonElement parameters,
        AgentJobContext job,
        IServiceProvider scopedServices,
        CancellationToken ct) =>
        Task.FromResult("{}");
}
