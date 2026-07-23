using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharpClaw.Contracts.DTOs.AgentActions;
using SharpClaw.Contracts.Enums;
using SharpClaw.Contracts.Modules;
using SharpClaw.Contracts.Modules.Foreign;

internal static class OutOfProcessHostCapabilityProxies
{
    public static void Register(IServiceCollection services)
    {
        var client = OutOfProcessHostCapabilityClient.TryCreateFromEnvironment();
        if (client is null)
            return;

        services.TryAddSingleton(client);
        services.TryAddSingleton<IModuleConfigStore, ModuleConfigStoreProxy>();
        services.TryAddSingleton<ICoreEntityIdProvider, CoreEntityIdProviderProxy>();
        services.TryAddSingleton<IAgentManager, AgentManagerProxy>();
        services.TryAddSingleton<IModelInfoProvider, ModelInfoProviderProxy>();
        services.TryAddSingleton<IModelRegistrar, ModelRegistrarProxy>();
        services.TryAddSingleton<IModuleInfoProvider, ModuleInfoProviderProxy>();
        services.TryAddSingleton<IModuleLifecycleManager, ModuleLifecycleManagerProxy>();
        services.TryAddSingleton<IForeignModuleProtocolContractResolver, ProtocolContractResolverProxy>();
        services.TryAddSingleton<IModuleStorageGateway, ModuleStorageGatewayProxy>();
        services.TryAddSingleton<IAgentJobController, AgentJobControllerProxy>();
        services.TryAddSingleton<IAgentJobReader, AgentJobReaderProxy>();
    }

    private sealed class ModuleConfigStoreProxy(OutOfProcessHostCapabilityClient client) : IModuleConfigStore
    {
        public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleConfigGetRequest, ForeignModuleConfigGetResponse>(
                ForeignModuleHostCapabilityProtocol.ConfigGetPath,
                new ForeignModuleConfigGetRequest { Key = key },
                ct)).Value;

        public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
            where T : IParsable<T>
        {
            var value = await GetAsync(key, ct);
            return value is null || !T.TryParse(value, null, out var parsed)
                ? default
                : parsed;
        }

        public Task SetAsync(string key, string? value, CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.ConfigSetPath,
                new ForeignModuleConfigSetRequest { Key = key, Value = value },
                ct);

        public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
            (await client.PostAsync<object, ForeignModuleConfigAllResponse>(
                ForeignModuleHostCapabilityProtocol.ConfigAllPath,
                new { },
                ct)).Values;
    }

    private sealed class CoreEntityIdProviderProxy(OutOfProcessHostCapabilityClient client) : ICoreEntityIdProvider
    {
        public async Task<List<Guid>> GetAgentIdsAsync(CancellationToken ct = default) =>
            [.. (await client.PostAsync<object, ForeignModuleIdsResponse>(
                ForeignModuleHostCapabilityProtocol.CoreAgentIdsPath,
                new { },
                ct)).Ids];

        public async Task<List<Guid>> GetChannelIdsAsync(CancellationToken ct = default) =>
            [.. (await client.PostAsync<object, ForeignModuleIdsResponse>(
                ForeignModuleHostCapabilityProtocol.CoreChannelIdsPath,
                new { },
                ct)).Ids];

        public async Task<List<(Guid Id, string Name)>> GetAgentLookupItemsAsync(CancellationToken ct = default) =>
            [.. (await client.PostAsync<object, ForeignModuleLookupItemsResponse>(
                ForeignModuleHostCapabilityProtocol.CoreAgentLookupPath,
                new { },
                ct)).Items.Select(item => (item.Id, item.Name))];

        public async Task<List<(Guid Id, string Name)>> GetChannelLookupItemsAsync(CancellationToken ct = default) =>
            [.. (await client.PostAsync<object, ForeignModuleLookupItemsResponse>(
                ForeignModuleHostCapabilityProtocol.CoreChannelLookupPath,
                new { },
                ct)).Items.Select(item => (item.Id, item.Name))];
    }

    private sealed class AgentManagerProxy(OutOfProcessHostCapabilityClient client) : IAgentManager
    {
        public async Task<(Guid AgentId, string ModelName, string AgentName)> CreateSubAgentAsync(
            string name,
            Guid modelId,
            string? systemPrompt,
            CancellationToken ct = default)
        {
            var response = await client.PostAsync<ForeignModuleAgentCreateRequest, ForeignModuleAgentCreateResponse>(
                ForeignModuleHostCapabilityProtocol.AgentCreateSubAgentPath,
                new ForeignModuleAgentCreateRequest
                {
                    Name = name,
                    ModelId = modelId,
                    SystemPrompt = systemPrompt,
                },
                ct);
            return (response.AgentId, response.ModelName, response.AgentName);
        }

        public async Task<string> UpdateAgentAsync(
            Guid agentId,
            string? name,
            string? systemPrompt,
            Guid? modelId,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleAgentUpdateRequest, ForeignModuleAgentUpdateResponse>(
                ForeignModuleHostCapabilityProtocol.AgentUpdatePath,
                new ForeignModuleAgentUpdateRequest
                {
                    AgentId = agentId,
                    Name = name,
                    SystemPrompt = systemPrompt,
                    ModelId = modelId,
                },
                ct)).Result;

        public Task SetAgentHeaderAsync(Guid agentId, string? header, CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.AgentSetHeaderPath,
                new ForeignModuleSetHeaderRequest { Id = agentId, Header = header },
                ct);

        public Task SetChannelHeaderAsync(Guid channelId, string? header, CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.ChannelSetHeaderPath,
                new ForeignModuleSetHeaderRequest { Id = channelId, Header = header },
                ct);
    }

    private sealed class ModuleInfoProviderProxy(OutOfProcessHostCapabilityClient client) : IModuleInfoProvider
    {
        public IReadOnlyList<ModuleInfo> GetAllModules() =>
            client.PostAsync<object, ForeignModuleInfoListResponse>(
                    ForeignModuleHostCapabilityProtocol.ModulesInfoListPath,
                    new { },
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Modules;
    }

    private sealed class ModelInfoProviderProxy(OutOfProcessHostCapabilityClient client) : IModelInfoProvider
    {
        public async Task<ModelProviderInfo?> GetModelProviderInfoAsync(
            Guid modelId,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleModelMetadataRequest, ForeignModuleModelProviderInfoResponse>(
                ForeignModuleHostCapabilityProtocol.ModelProviderInfoPath,
                new ForeignModuleModelMetadataRequest { ModelId = modelId },
                ct)).Info;

        public async Task<string?> GetLocalModelFilePathAsync(
            Guid modelId,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleModelMetadataRequest, ForeignModuleModelLocalFilePathResponse>(
                ForeignModuleHostCapabilityProtocol.ModelLocalFilePathPath,
                new ForeignModuleModelMetadataRequest { ModelId = modelId },
                ct)).Path;
    }

    private sealed class ModelRegistrarProxy(OutOfProcessHostCapabilityClient client) : IModelRegistrar
    {
        public async Task<Guid> EnsureProviderAsync(
            string providerKey,
            string displayName,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleModelEnsureProviderRequest, ForeignModuleGuidResponse>(
                ForeignModuleHostCapabilityProtocol.ModelEnsureProviderPath,
                new ForeignModuleModelEnsureProviderRequest
                {
                    ProviderKey = providerKey,
                    DisplayName = displayName,
                },
                ct)).Id;

        public async Task<Guid> EnsureModelAsync(
            string modelName,
            Guid providerId,
            IReadOnlyList<string> capabilityTags,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleModelEnsureModelRequest, ForeignModuleGuidResponse>(
                ForeignModuleHostCapabilityProtocol.ModelEnsureModelPath,
                new ForeignModuleModelEnsureModelRequest
                {
                    ModelName = modelName,
                    ProviderId = providerId,
                    CapabilityTags = capabilityTags,
                },
                ct)).Id;

        public async Task<ModelMetadata?> GetModelMetadataAsync(
            Guid modelId,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleModelMetadataRequest, ForeignModuleModelMetadataResponse>(
                ForeignModuleHostCapabilityProtocol.ModelMetadataPath,
                new ForeignModuleModelMetadataRequest { ModelId = modelId },
                ct)).Metadata;

        public async Task<bool> DeleteModelAsync(Guid modelId, CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleModelDeleteRequest, ForeignModuleBooleanResponse>(
                ForeignModuleHostCapabilityProtocol.ModelDeletePath,
                new ForeignModuleModelDeleteRequest { ModelId = modelId },
                ct)).Value;
    }

    private sealed class ModuleLifecycleManagerProxy(OutOfProcessHostCapabilityClient client) : IModuleLifecycleManager
    {
        private readonly RemoteToolModule _remoteToolModule = new(client);
        private string? _externalModulesDir;

        public string ExternalModulesDir =>
            _externalModulesDir ??= client.PostAsync<object, ForeignModuleExternalModulesRootResponse>(
                    ForeignModuleHostCapabilityProtocol.ModulesExternalRootPath,
                    new { },
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Directory;

        public bool IsModuleRegistered(string moduleId) =>
            client.PostAsync<ForeignModuleRegisteredRequest, ForeignModuleRegisteredResponse>(
                    ForeignModuleHostCapabilityProtocol.ModuleRegisteredPath,
                    new ForeignModuleRegisteredRequest { ModuleId = moduleId },
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .IsRegistered;

        public bool IsToolPrefixRegistered(string toolPrefix) =>
            client.PostAsync<ForeignModuleToolPrefixRegisteredRequest, ForeignModuleRegisteredResponse>(
                    ForeignModuleHostCapabilityProtocol.ModuleToolPrefixRegisteredPath,
                    new ForeignModuleToolPrefixRegisteredRequest { ToolPrefix = toolPrefix },
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .IsRegistered;

        public (ISharpClawCoreModule Module, string ToolName)? FindToolByName(string toolName) =>
            (_remoteToolModule, toolName);

        public async Task<ModuleStateResponse> LoadExternalAsync(
            string moduleDir,
            IServiceProvider hostServices,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleLoadRequest, ForeignModuleStateResponseEnvelope>(
                ForeignModuleHostCapabilityProtocol.ModuleLoadPath,
                new ForeignModuleLoadRequest { ModuleDir = moduleDir },
                ct)).State;

        public Task UnloadExternalAsync(string moduleId, CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.ModuleUnloadPath,
                new ForeignModuleModuleIdRequest { ModuleId = moduleId },
                ct);

        public async Task<ModuleStateResponse> ReloadExternalAsync(
            string moduleId,
            IServiceProvider hostServices,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleModuleIdRequest, ForeignModuleStateResponseEnvelope>(
                ForeignModuleHostCapabilityProtocol.ModuleReloadPath,
                new ForeignModuleModuleIdRequest { ModuleId = moduleId },
                ct)).State;
    }

    private sealed class ProtocolContractResolverProxy(OutOfProcessHostCapabilityClient client)
        : IForeignModuleProtocolContractResolver
    {
        public IForeignModuleProtocolContractInvoker? Resolve(string contractName)
        {
            var export = GetAllExports()
                .FirstOrDefault(candidate => string.Equals(candidate.ContractName, contractName, StringComparison.Ordinal));
            return export is null ? null : new ProtocolContractInvokerProxy(client, export);
        }

        public IReadOnlyList<ForeignModuleProtocolContractExport> GetAllExports() =>
            client.PostAsync<object, ForeignModuleProtocolContractsListResponse>(
                    ForeignModuleHostCapabilityProtocol.ProtocolContractsListPath,
                    new { },
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Contracts;
    }

    private sealed class ModuleStorageGatewayProxy(OutOfProcessHostCapabilityClient client) : IModuleStorageGateway
    {
        public IReadOnlyList<ModuleStorageContractDescriptor> ListContracts() =>
            client.PostAsync<object, ForeignModuleStorageContractsResponse>(
                    ForeignModuleHostCapabilityProtocol.ModuleStorageListPath,
                    new { },
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Contracts;

        public async Task<JsonElement> InvokeAsync(
            string moduleId,
            string storageName,
            string operation,
            JsonElement parameters,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleStorageInvokeRequest, ForeignModuleStorageInvokeResponse>(
                ForeignModuleHostCapabilityProtocol.ModuleStorageInvokePath,
                new ForeignModuleStorageInvokeRequest
                {
                    ModuleId = moduleId,
                    StorageName = storageName,
                    Operation = operation,
                    Parameters = parameters,
                },
                ct)).Result;
    }

    private sealed class AgentJobControllerProxy(OutOfProcessHostCapabilityClient client) : IAgentJobController
    {
        public Task<AgentJobResponse> SubmitJobAsync(
            Guid channelId,
            SubmitAgentJobRequest request,
            CancellationToken ct = default) =>
            throw new NotSupportedException("Submitting new jobs from a .NET out-of-process module is not exposed yet.");

        public Task<AgentJobDetailResponse?> StopJobAsync(
            Guid jobId,
            string? requiredActionPrefix = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException("Stopping arbitrary jobs from a .NET out-of-process module is not exposed yet.");

        public Task AddJobLogAsync(
            Guid jobId,
            string message,
            string level = JobLogLevels.Info,
            CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.JobLogPath,
                new ForeignModuleJobLogRequest
                {
                    JobId = jobId,
                    Message = message,
                    Level = level,
                },
                ct);

        public Task MarkJobCompletedAsync(
            Guid jobId,
            string? resultData = null,
            string? message = null,
            CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.JobCompletePath,
                new ForeignModuleJobCompleteRequest
                {
                    JobId = jobId,
                    ResultData = resultData,
                    Message = message,
                },
                ct);

        public Task MarkJobFailedAsync(Guid jobId, Exception exception, CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.JobFailPath,
                new ForeignModuleJobFailRequest
                {
                    JobId = jobId,
                    Message = exception.Message,
                    Details = exception.ToString(),
                },
                ct);

        public Task MarkJobFailedAsync(
            Guid jobId,
            string message,
            string? details = null,
            CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.JobFailPath,
                new ForeignModuleJobFailRequest
                {
                    JobId = jobId,
                    Message = message,
                    Details = details,
                },
                ct);

        public Task MarkJobCancelledAsync(
            Guid jobId,
            string? message = null,
            CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.JobCancelPath,
                new ForeignModuleJobCancelRequest
                {
                    JobId = jobId,
                    Message = message,
                },
                ct);

        public Task CancelStaleJobsByActionPrefixAsync(string actionKeyPrefix, CancellationToken ct = default) =>
            client.PostAckAsync(
                ForeignModuleHostCapabilityProtocol.JobCancelStaleByActionPrefixPath,
                new ForeignModuleJobActionPrefixRequest { ActionKeyPrefix = actionKeyPrefix },
                ct);
    }

    private sealed class AgentJobReaderProxy(OutOfProcessHostCapabilityClient client) : IAgentJobReader
    {
        public async Task<AgentJobDetailResponse?> GetJobAsync(
            Guid jobId,
            CancellationToken ct = default) =>
            (await client.PostAsync<object, ForeignModuleJobGetResponse>(
                ForeignModuleHostCapabilityProtocol.JobGetPath,
                new { Id = jobId },
                ct)).Job;

        public async Task<AgentJobSummaryPageResponse> ListJobSummariesByActionPrefixAsync(
            string actionKeyPrefix,
            Guid? resourceId = null,
            string? cursor = null,
            int take = 50,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleJobActionPrefixRequest, ForeignModuleJobSummaryPageResponse>(
                ForeignModuleHostCapabilityProtocol.JobListSummariesByActionPrefixPath,
                new ForeignModuleJobActionPrefixRequest
                {
                    ActionKeyPrefix = actionKeyPrefix,
                    ResourceId = resourceId,
                    Cursor = cursor,
                    Take = take,
                },
                ct)).Page;

        public async Task<bool> JobExistsWithActionPrefixAsync(
            Guid jobId,
            string actionKeyPrefix,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleJobExistsWithActionPrefixRequest, ForeignModuleBooleanResponse>(
                ForeignModuleHostCapabilityProtocol.JobExistsWithActionPrefixPath,
                new ForeignModuleJobExistsWithActionPrefixRequest
                {
                    JobId = jobId,
                    ActionKeyPrefix = actionKeyPrefix,
                },
                ct)).Value;
    }

    private sealed class ProtocolContractInvokerProxy(
        OutOfProcessHostCapabilityClient client,
        ForeignModuleProtocolContractExport export) : IForeignModuleProtocolContractInvoker
    {
        public string ContractName => export.ContractName;
        public IReadOnlyList<ForeignModuleProtocolContractOperation> Operations => export.Operations;

        public async Task<JsonElement> InvokeAsync(
            string operation,
            JsonElement parameters,
            CancellationToken ct = default) =>
            (await client.PostAsync<ForeignModuleProtocolContractInvokeRequest, ForeignModuleProtocolContractInvokeResponse>(
                ForeignModuleHostCapabilityProtocol.ProtocolContractInvokePath,
                new ForeignModuleProtocolContractInvokeRequest
                {
                    ContractName = ContractName,
                    Operation = operation,
                    Parameters = parameters,
                },
                ct)).Result;
    }

    private sealed class RemoteToolModule(OutOfProcessHostCapabilityClient client) : ISharpClawCoreModule
    {
        public string Id => "sharpclaw_host_tools";
        public string DisplayName => "SharpClaw Host Tools";
        public string ToolPrefix => "host";

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
            InvokeAsync(toolName, parameters, ct);

        private async Task<string> InvokeAsync(string toolName, JsonElement parameters, CancellationToken ct) =>
            (await client.PostAsync<ForeignModuleToolInvokeRequest, ForeignModuleToolInvokeResponse>(
                ForeignModuleHostCapabilityProtocol.ModuleToolInvokePath,
                new ForeignModuleToolInvokeRequest
                {
                    ToolName = toolName,
                    Parameters = parameters,
                },
                ct)).Result;
    }
}

internal sealed class OutOfProcessHostCapabilityClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 32,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _httpClient;
    private readonly string _token;

    private OutOfProcessHostCapabilityClient(Uri address, string token)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = address,
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _token = token;
    }

    public static OutOfProcessHostCapabilityClient? TryCreateFromEnvironment()
    {
        var address = Environment.GetEnvironmentVariable(ForeignModuleHostCapabilityProtocol.AddressEnv);
        var token = Environment.GetEnvironmentVariable(ForeignModuleHostCapabilityProtocol.TokenEnv);
        return string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(token)
            ? null
            : new OutOfProcessHostCapabilityClient(new Uri(address), token);
    }

    public async Task PostAckAsync<TRequest>(string path, TRequest request, CancellationToken ct = default) =>
        _ = await PostAsync<TRequest, ForeignModuleCapabilityAck>(path, request, ct);

    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.TryAddWithoutValidation(ForeignModuleProtocol.TokenHeaderName, _token);

        using var response = await _httpClient.SendAsync(message, ct);
        var body = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"SharpClaw host capability call {path} failed with HTTP {(int)response.StatusCode}: {body}");
        }

        if (string.IsNullOrWhiteSpace(body))
            return Activator.CreateInstance<TResponse>();

        return JsonSerializer.Deserialize<TResponse>(body, JsonOptions)
            ?? throw new JsonException($"Host capability call {path} returned invalid JSON.");
    }
}
