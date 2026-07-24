using System.Net;
using System.Text;
using System.Text.Json;
using BepInEx.Logging;
using MemoryOfMemorieCodexBridge.Commands;
using MemoryOfMemorieCodexBridge.Probing;

namespace MemoryOfMemorieCodexBridge.Api;

internal sealed class LocalApiServer
{
    private const string Prefix = "http://127.0.0.1:29461/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpListener listener = new();
    private readonly ManualLogSource log;
    private readonly GameCommandDispatcher commands;
    private readonly RuntimeProbe probe;
    private readonly CancellationTokenSource shutdown = new();

    internal LocalApiServer(RuntimeProbe probe, GameCommandDispatcher commands, ManualLogSource log)
    {
        this.probe = probe;
        this.commands = commands;
        this.log = log;
        listener.Prefixes.Add(Prefix);
    }

    internal void Start()
    {
        listener.Start();
        _ = Task.Run(ListenAsync);
        log.LogInfo($"Local API listening on {Prefix}");
    }

    private async Task ListenAsync()
    {
        while (!shutdown.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync();
                HandleWithoutAwait(context);
            }
            catch (HttpListenerException) when (shutdown.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                log.LogError(exception);
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var response = await CreateResponseAsync(context.Request);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = response.StatusCode;
        var payload = JsonSerializer.Serialize(response.Body, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(payload);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async void HandleWithoutAwait(HttpListenerContext context)
    {
        try
        {
            await HandleAsync(context);
        }
        catch (Exception exception)
        {
            log.LogError(exception);
        }
    }

    private async Task<ApiHttpResponse> CreateResponseAsync(HttpListenerRequest request)
    {
        if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/health")
        {
            return new ApiHttpResponse(200, ApiResponse<object>.Success(new { plugin = PluginInfo.Name, version = PluginInfo.Version, mode = "pomodoro_ui_bridge" }));
        }

        if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/v1/capabilities")
        {
            return new ApiHttpResponse(200, ApiResponse<IReadOnlyList<CapabilityProbe>>.Success(probe.Capture().Capabilities));
        }

        if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/v1/probe")
        {
            return new ApiHttpResponse(200, ApiResponse<RuntimeSnapshot>.Success(probe.Capture()));
        }

        if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/v1/timer-status")
        {
            return new ApiHttpResponse(200, ApiResponse<TimerStatusSnapshot>.Success(await commands.CaptureTimerStatusAsync()));
        }

        if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/v1/commands")
        {
            return await HandleCommandAsync(request);
        }

        return new ApiHttpResponse(404, ApiResponse<object>.Failure("Endpoint not found."));
    }

    private async Task<ApiHttpResponse> HandleCommandAsync(HttpListenerRequest request)
    {
        CommandRequest commandRequest;
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            commandRequest = JsonSerializer.Deserialize<CommandRequest>(body, JsonOptions);
        }
        catch (JsonException exception)
        {
            return new ApiHttpResponse(400, ApiResponse<object>.Failure($"Invalid JSON: {exception.Message}"));
        }

        var commandId = commandRequest == null ? null : commandRequest.CommandId;
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return new ApiHttpResponse(400, ApiResponse<object>.Failure("Missing command id. Use JSON like { \"id\": \"pomodoro.ui-start\" }."));
        }

        var result = await commands.ExecuteAsync(commandId, commandRequest == null ? 0 : commandRequest.Minutes);
        return result.Success
            ? new ApiHttpResponse(200, ApiResponse<CommandExecutionResult>.Success(result))
            : new ApiHttpResponse(409, ApiResponse<CommandExecutionResult>.Failure(result.Message));
    }
}
