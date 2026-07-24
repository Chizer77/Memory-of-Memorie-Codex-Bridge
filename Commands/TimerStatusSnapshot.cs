namespace MemoryOfMemorieCodexBridge.Commands;

internal sealed class TimerStatusSnapshot
{
    internal TimerStatusSnapshot(
        bool pomodoroFound,
        string currentState,
        int workMinutes,
        int workSec,
        int workCount,
        double totalWorkTime,
        long storedTotalWorkSec,
        int storedTotalWorkRunCount,
        string error)
    {
        PomodoroFound = pomodoroFound;
        CurrentState = currentState;
        WorkMinutes = workMinutes;
        WorkSec = workSec;
        WorkCount = workCount;
        TotalWorkTime = totalWorkTime;
        StoredTotalWorkSec = storedTotalWorkSec;
        StoredTotalWorkRunCount = storedTotalWorkRunCount;
        Error = error;
    }

    public bool PomodoroFound { get; }

    public string CurrentState { get; }

    public int WorkMinutes { get; }

    public int WorkSec { get; }

    public int WorkCount { get; }

    public double TotalWorkTime { get; }

    public long StoredTotalWorkSec { get; }

    public int StoredTotalWorkRunCount { get; }

    public string Error { get; }
}
