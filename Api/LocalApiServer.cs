using System.Net;
using System.Text;
using System.Text.Json;
using BepInEx.Logging;
using MemoryOfMemorieCodexBridge.Game.Pomodoro;
using MemoryOfMemorieCodexBridge.Probing;

namespace MemoryOfMemorieCodexBridge.Api;

internal sealed class LocalApiServer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpListener listener = new();
    private readonly ManualLogSource log;
    private readonly PomodoroController pomodoro;
    private readonly RuntimeProbe probe;
    private readonly object lifecycleLock = new();
    private readonly string prefix;
    private CancellationTokenSource listeningCancellation;
    private bool isListening;

    internal LocalApiServer(RuntimeProbe probe, PomodoroController pomodoro, ManualLogSource log, string listenUrl)
    {
        this.probe = probe;
        this.pomodoro = pomodoro;
        this.log = log;
        prefix = NormalizePrefix(listenUrl);
        listener.Prefixes.Add(prefix);
    }

    internal void Start()
    {
        CancellationToken cancellationToken;
        lock (lifecycleLock)
        {
            if (isListening) return;
            listener.Start();
            isListening = true;
            listeningCancellation = new CancellationTokenSource();
            cancellationToken = listeningCancellation.Token;
        }

        _ = ListenAsync(cancellationToken);
        log.LogInfo($"Local API listening on {prefix}");
    }

    internal void Stop()
    {
        CancellationTokenSource cancellation;
        lock (lifecycleLock)
        {
            if (!isListening) return;
            isListening = false;
            cancellation = listeningCancellation;
            listeningCancellation = null;
            cancellation.Cancel();
            listener.Stop();
        }

        // 停止监听会解除 GetContextAsync 的等待，使旧监听循环立即退出。
        cancellation.Dispose();
        log.LogInfo("Local API stopped.");
    }

    internal bool IsListening
    {
        get
        {
            lock (lifecycleLock) return isListening;
        }
    }

    private static string NormalizePrefix(string listenUrl)
    {
        if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException("ListenUrl must be an absolute HTTP URL.", nameof(listenUrl));
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
        {
            throw new ArgumentException("ListenUrl must not contain a path, query, or fragment.", nameof(listenUrl));
        }

        // HttpListener 使用根前缀，避免配置路径与固定 API 路由发生错配。
        return uri.GetLeftPart(UriPartial.Authority) + "/";
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync();
                if (cancellationToken.IsCancellationRequested) return;
                HandleWithoutAwait(context);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
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
            return new ApiHttpResponse(200, ApiResponse<PomodoroStatusSnapshot>.Success(await pomodoro.CaptureStatusAsync()));
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

        var result = await pomodoro.ExecuteAsync(commandId, commandRequest == null ? 0 : commandRequest.Minutes);
        return result.Success
            ? new ApiHttpResponse(200, ApiResponse<PomodoroCommandResult>.Success(result))
            : new ApiHttpResponse(409, ApiResponse<PomodoroCommandResult>.Failure(result.Message));
    }
}
