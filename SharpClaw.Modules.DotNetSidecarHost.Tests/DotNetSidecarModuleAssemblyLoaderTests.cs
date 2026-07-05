using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharpClaw.Contracts.Modules;
using SharpClaw.Core.Modules;

namespace SharpClaw.Modules.DotNetSidecarHost.Tests;

public sealed class DotNetSidecarModuleAssemblyLoaderTests
{
    [Test]
    public void CreateModuleInstanceUsesExplicitModuleType()
    {
        var manifest = Manifest("loader_other_module");
        var runtimeInfo = new ModuleManifestRuntimeInfo(
            ModuleManifestRuntimeInfo.DotNet,
            Entrypoint: null,
            ModuleType: typeof(LoaderSampleModule).FullName);

        var module = DotNetSidecarModuleAssemblyLoader.CreateModuleInstance(
            Assembly.GetExecutingAssembly(),
            manifest,
            runtimeInfo,
            "SharpClaw.Modules.DotNetSidecarHost.Tests.dll");

        module.Id.Should().Be("loader_sample_module");
    }

    [Test]
    public void CreateModuleInstanceFallsBackToManifestId()
    {
        var manifest = Manifest("loader_sample_module");

        var module = DotNetSidecarModuleAssemblyLoader.CreateModuleInstance(
            Assembly.GetExecutingAssembly(),
            manifest,
            ModuleManifestRuntimeInfo.DotNetDefault,
            "SharpClaw.Modules.DotNetSidecarHost.Tests.dll");

        module.Should().BeOfType<LoaderSampleModule>();
    }

    [Test]
    public void CreateModuleInstanceRejectsMissingManifestId()
    {
        var manifest = Manifest("missing_module");

        var act = () => DotNetSidecarModuleAssemblyLoader.CreateModuleInstance(
            Assembly.GetExecutingAssembly(),
            manifest,
            ModuleManifestRuntimeInfo.DotNetDefault,
            "SharpClaw.Modules.DotNetSidecarHost.Tests.dll");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares module id 'missing_module'*");
    }

    [Test]
    public void PathGuardRejectsDirectoryTraversal()
    {
        var parent = Path.Combine(Path.GetTempPath(), "sharpclaw-sidecar-parent");
        var escaped = Path.Combine(parent, "..", "outside", "module.json");

        var act = () => DotNetSidecarPathGuard.EnsureContainedIn(escaped, parent);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*escapes the allowed directory*");
    }

    [Test]
    public void ManifestJsonOptionsRejectExcessiveDepth()
    {
        var tooDeep = "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":{\"i\":1}}}}}}}}}";

        var act = () => JsonSerializer.Deserialize<Dictionary<string, object?>>(
            tooDeep,
            DotNetSidecarJsonOptions.Manifest);

        act.Should().Throw<JsonException>();
    }

    private static ModuleManifest Manifest(string id) =>
        new(
            id,
            DisplayName: "Loader Sample",
            Version: "1.0.0",
            ToolPrefix: "loader",
            EntryAssembly: "Loader.dll",
            MinHostVersion: "0.0.0");

    private sealed class LoaderSampleModule : ISharpClawCoreModule
    {
        public string Id => "loader_sample_module";
        public string DisplayName => "Loader Sample";
        public string ToolPrefix => "loader";

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
}
