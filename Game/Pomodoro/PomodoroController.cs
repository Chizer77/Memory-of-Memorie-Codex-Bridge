using System.Reflection;
using BepInEx.Logging;
using MemoryOfMemorieCodexBridge.Game.Ui;

namespace MemoryOfMemorieCodexBridge.Game.Pomodoro;

internal sealed class PomodoroController
{
    private const string PomodoroTimerViewTypeName = "App.UI.PomodoroTimer.PomodoroTimerView";
    // 主场景 UI 在首帧后仍可能继续初始化，给首次 Hook 足够的原生界面就绪时间。
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private readonly UnityMainThreadQueue mainThreadQueue;
    private readonly GameUiVisibilityController gameUi;
    private readonly ManualLogSource log;

    internal PomodoroController(UnityMainThreadQueue mainThreadQueue, GameUiVisibilityController gameUi, ManualLogSource log)
    {
        this.mainThreadQueue = mainThreadQueue;
        this.gameUi = gameUi;
        this.log = log;
    }

    internal async Task<PomodoroCommandResult> ExecuteAsync(string commandId, int minutes)
    {
        if (!IsSupported(commandId))
        {
            return PomodoroCommandResult.Failed(commandId, "Unsupported command. Supported commands: pomodoro.set-work-minutes, pomodoro.ui-start, pomodoro.ui-stop.");
        }

        if (commandId == "pomodoro.set-work-minutes" && minutes <= 0)
        {
            return PomodoroCommandResult.Failed(commandId, $"{commandId} requires a positive minutes value.");
        }

        if (commandId is "pomodoro.set-work-minutes" or "pomodoro.ui-start" or "pomodoro.ui-stop")
        {
            // 设置与启动属于同一工作流，设置后保持 UI 可见，避免两次请求之间发生闪隐。
            var uiVisible = gameUi.ShowForInteraction(commandId == "pomodoro.set-work-minutes" ? CommandTimeout : null);
            var visibilityCompleted = await Task.WhenAny(uiVisible, Task.Delay(CommandTimeout));
            if (visibilityCompleted != uiVisible)
            {
                return PomodoroCommandResult.Failed(commandId, "Game UI did not become ready before the command timeout.");
            }
            await Task.Delay(GameUiVisibilityController.InteractionRevealDelay);
        }

        var operation = mainThreadQueue.Enqueue(() => ExecuteOnUnityThread(commandId, minutes));
        var completed = await Task.WhenAny(operation, Task.Delay(CommandTimeout));
        if (completed != operation)
        {
            return PomodoroCommandResult.Failed(commandId, "Command timed out before Unity main-thread execution completed.");
        }

        return await operation;
    }

    internal async Task<PomodoroStatusSnapshot> CaptureStatusAsync()
    {
        var operation = mainThreadQueue.Enqueue(CaptureStatusOnUnityThread);
        var completed = await Task.WhenAny(operation, Task.Delay(CommandTimeout));
        if (completed != operation)
        {
            return new PomodoroStatusSnapshot(false, string.Empty, 0, 0, 0, 0, 0, 0, "Timer status probe timed out.");
        }

        return await operation;
    }

    private PomodoroCommandResult ExecuteOnUnityThread(string commandId, int minutes)
    {
        try
        {
            if (!TryFindTimer(out var target, out var targetType, out var error))
            {
                return PomodoroCommandResult.Failed(commandId, error);
            }

            var result = commandId switch
            {
                "pomodoro.ui-start" or "pomodoro.ui-stop" => PressPomodoroButton(commandId, target, targetType),
                "pomodoro.set-work-minutes" => SetWorkMinutes(commandId, target, targetType, minutes),
                _ => PomodoroCommandResult.Failed(commandId, "Unsupported command.")
            };

            if (result.Success) log.LogInfo($"Executed command {commandId} through the native Pomodoro UI.");
            return result;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            log.LogError(exception.InnerException);
            return PomodoroCommandResult.Failed(commandId, exception.InnerException.Message);
        }
        catch (Exception exception)
        {
            log.LogError(exception);
            return PomodoroCommandResult.Failed(commandId, exception.Message);
        }
    }

    private static bool TryFindTimer(out object target, out Type targetType, out string error)
    {
        target = null;
        targetType = Il2CppReflection.FindType(PomodoroTimerViewTypeName);
        if (targetType is null)
        {
            error = $"Type not loaded: {PomodoroTimerViewTypeName}.";
            return false;
        }

        var found = Il2CppReflection.FindUnityObject(targetType);
        if (found is null)
        {
            error = $"No live {PomodoroTimerViewTypeName} instance was found in the current scene.";
            return false;
        }

        target = Il2CppReflection.WrapAsTargetType(found, targetType);
        error = target is null ? $"Found object could not be wrapped as {PomodoroTimerViewTypeName}." : string.Empty;
        return target is not null;
    }

    private static PomodoroCommandResult SetWorkMinutes(string commandId, object target, Type targetType, int minutes)
    {
        var currentState = Il2CppReflection.ReadInstanceString(target, targetType, "CurrentState");
        if (!string.IsNullOrWhiteSpace(currentState) && currentState != "Default")
        {
            return PomodoroCommandResult.Failed(commandId, $"Work minutes can only be changed before starting the timer. Current state: {currentState}.");
        }

        Il2CppReflection.SetInstanceInt(target, targetType, "m_workTime", minutes);
        Il2CppReflection.SetText(Il2CppReflection.ReadInstanceProperty(target, targetType, "m_workTimeText"), minutes.ToString());

        var settingUi = Il2CppReflection.ReadInstanceProperty(target, targetType, "m_uiTimerSetting");
        if (settingUi is not null)
        {
            var settingType = settingUi.GetType();
            Il2CppReflection.SetInstanceInt(settingUi, settingType, "m_workTime", minutes);
            Il2CppReflection.SetText(Il2CppReflection.ReadInstanceProperty(settingUi, settingType, "m_workTimeText"), minutes.ToString());
            Il2CppReflection.InvokeInstanceVoid(settingUi, settingType, "SaveSettings");
        }

        Il2CppReflection.InvokeInstanceVoid(target, targetType, "RefreshSettings");
        Il2CppReflection.SetInstanceInt(target, targetType, "m_workTime", minutes);
        Il2CppReflection.SetText(Il2CppReflection.ReadInstanceProperty(target, targetType, "m_workTimeText"), minutes.ToString());
        Il2CppReflection.InvokeInstanceVoid(target, targetType, "SetTime", minutes, 0d);
        Il2CppReflection.InvokeInstanceVoid(target, targetType, "SavePomodoroSettings");
        return PomodoroCommandResult.Completed(commandId, $"Set work minutes to {minutes}.");
    }

    private static PomodoroCommandResult PressPomodoroButton(string commandId, object target, Type targetType)
    {
        var button = commandId == "pomodoro.ui-start"
            ? Il2CppReflection.ReadInstanceProperty(target, targetType, "m_startBtn")
            : FindFirstInteractableStopButton(target, targetType);
        if (button is null) return PomodoroCommandResult.Failed(commandId, $"No button found for {commandId}.");

        var buttonType = button.GetType();
        var press = buttonType.GetMethod("Press", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (press is not null)
        {
            press.Invoke(button, null);
            return PomodoroCommandResult.Completed(commandId, "Pressed Unity UI Button.Press().");
        }

        var onClick = Il2CppReflection.ReadInstanceProperty(button, buttonType, "onClick");
        var invoke = onClick?.GetType().GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (invoke is null) return PomodoroCommandResult.Failed(commandId, "Button had no Press() or onClick.Invoke() method.");

        invoke.Invoke(onClick, null);
        return PomodoroCommandResult.Completed(commandId, "Invoked Unity UI Button.onClick.");
    }

    private static object FindFirstInteractableStopButton(object target, Type targetType)
    {
        if (Il2CppReflection.ReadInstanceProperty(target, targetType, "m_stopBtns") is System.Collections.IEnumerable buttons)
        {
            foreach (var button in buttons)
            {
                if (button is not null && IsInteractable(button)) return button;
            }
        }
        return Il2CppReflection.ReadInstanceProperty(target, targetType, "m_skipBtn");
    }

    private static bool IsInteractable(object button)
    {
        var method = button.GetType().GetMethod("IsInteractable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        return method?.Invoke(button, null) as bool? ?? true;
    }

    private static PomodoroStatusSnapshot CaptureStatusOnUnityThread()
    {
        try
        {
            var found = TryFindTimer(out var target, out var targetType, out var error);
            var timerProviderType = Il2CppReflection.FindType("TimerSqliteProvider");
            var storedTotalWorkSec = Il2CppReflection.InvokeStaticLong(timerProviderType, "GetTotalWorkSec");
            return new PomodoroStatusSnapshot(found, Il2CppReflection.ReadInstanceString(target, targetType, "CurrentState"), ReadInt(target, targetType, "m_workTime"), ReadInt(target, targetType, "WorkSec"), ReadInt(target, targetType, "WorkCount"), ReadDouble(target, targetType, "m_totalWorkTime"), storedTotalWorkSec, Convert.ToInt32(Il2CppReflection.InvokeStaticLong(timerProviderType, "GetTotalWorkRunCount")), error);
        }
        catch (Exception exception)
        {
            return new PomodoroStatusSnapshot(false, string.Empty, 0, 0, 0, 0, 0, 0, exception.Message);
        }
    }

    private static int ReadInt(object target, Type type, string property) => int.TryParse(Il2CppReflection.ReadInstanceString(target, type, property), out var value) ? value : 0;
    private static double ReadDouble(object target, Type type, string property) => double.TryParse(Il2CppReflection.ReadInstanceString(target, type, property), out var value) ? value : 0;
    private static bool IsSupported(string commandId) => commandId is "pomodoro.set-work-minutes" or "pomodoro.ui-start" or "pomodoro.ui-stop";
}
