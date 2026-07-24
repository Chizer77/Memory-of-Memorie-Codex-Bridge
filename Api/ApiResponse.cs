namespace MemoryOfMemorieCodexBridge.Api;

internal sealed class ApiResponse<T>
{
    public ApiResponse(bool ok, T data, string error)
    {
        Ok = ok;
        Data = data;
        Error = error;
    }

    public bool Ok { get; }

    public T Data { get; }

    public string Error { get; }

    internal static ApiResponse<T> Success(T data) => new(true, data, null);

    internal static ApiResponse<T> Failure(string error) => new(false, default, error);
}
