namespace MemoryOfMemorieCodexBridge.Commands;

internal sealed class QueuedGameCommand
{
    private readonly TaskCompletionSource<CommandExecutionResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal QueuedGameCommand(string commandId, int minutes)
    {
        CommandId = commandId;
        Minutes = minutes;
    }

    internal string CommandId { get; }

    internal int Minutes { get; }

    internal Task<CommandExecutionResult> Completion => completion.Task;

    internal void Complete(CommandExecutionResult result)
    {
        completion.TrySetResult(result);
    }
}
