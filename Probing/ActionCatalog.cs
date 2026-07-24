namespace MemoryOfMemorieCodexBridge.Probing;

internal static class ActionCatalog
{
    internal static readonly IReadOnlyList<ActionDefinition> Candidates =
    [
        new("pomodoro.ui-start", "pomodoro", "UnityEngine.UI.Button", "Press", "Press the game's start button and run the same Pomodoro start flow as the UI."),
        new("pomodoro.ui-stop", "pomodoro", "UnityEngine.UI.Button", "Press", "Press the active stop button and run the same Pomodoro settlement flow as the UI."),
        new("pomodoro.set-work-minutes", "pomodoro", "App.UI.PomodoroTimer.TimerSettingUI", "SaveSettings", "Set and save the Pomodoro work-minute setting before starting through the UI path."),
        new("pomodoro.status", "pomodoro", "App.UI.PomodoroTimer.PomodoroTimerView", "get_CurrentState", "Read the live Pomodoro state used by /v1/timer-status.")
    ];
}
