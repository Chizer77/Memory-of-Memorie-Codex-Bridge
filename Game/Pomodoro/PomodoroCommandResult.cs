namespace MemoryOfMemorieCodexBridge.Game.Pomodoro;

internal sealed class PomodoroCommandResult
{
    internal PomodoroCommandResult(bool success, string command, string message)
    {
        Success = success;
        Command = command;
        Message = message;
    }

    public bool Success { get; }
    public string Command { get; }
    public string Message { get; }

    internal static PomodoroCommandResult Completed(string command, string message) => new(true, command, message);
    internal static PomodoroCommandResult Failed(string command, string message) => new(false, command, message);
}
