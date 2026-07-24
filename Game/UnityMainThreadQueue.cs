using System.Collections.Concurrent;

namespace MemoryOfMemorieCodexBridge.Game;

internal sealed class UnityMainThreadQueue
{
    private readonly ConcurrentQueue<Action> pendingOperations = new();

    internal Task<T> Enqueue<T>(Func<T> operation)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingOperations.Enqueue(() =>
        {
            try
            {
                completion.TrySetResult(operation());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    internal void Drain()
    {
        while (pendingOperations.TryDequeue(out var operation))
        {
            operation();
        }
    }
}
