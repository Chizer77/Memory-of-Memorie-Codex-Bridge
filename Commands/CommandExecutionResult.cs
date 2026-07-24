namespace MemoryOfMemorieCodexBridge.Commands;

internal sealed class CommandExecutionResult
{
    public CommandExecutionResult(bool success, string command, string message)
    {
        Success = success;
        Command = command;
        Message = message;
    }

    public bool Success { get; }

    public string Command { get; }

    public string Message { get; }

    internal static CommandExecutionResult Completed(string command, string message) => new(true, command, message);

    internal static CommandExecutionResult Failed(string command, string message) => new(false, command, message);
}
