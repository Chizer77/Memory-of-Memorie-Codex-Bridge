namespace MemoryOfMemorieCodexBridge.Api;

internal sealed class ApiHttpResponse
{
    internal ApiHttpResponse(int statusCode, object body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    internal int StatusCode { get; }

    internal object Body { get; }
}
