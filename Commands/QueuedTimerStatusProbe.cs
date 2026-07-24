namespace MemoryOfMemorieCodexBridge.Commands;

internal sealed class QueuedTimerStatusProbe
{
    private readonly TaskCompletionSource<TimerStatusSnapshot> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task<TimerStatusSnapshot> Completion => completion.Task;

    internal void Complete(TimerStatusSnapshot snapshot)
    {
        completion.TrySetResult(snapshot);
    }
}
