using System.Collections.Concurrent;
using System.Reflection;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace MemoryOfMemorieCodexBridge.Commands;

internal sealed class GameCommandDispatcher
{
    private const string PomodoroTimerViewTypeName = "App.UI.PomodoroTimer.PomodoroTimerView";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private readonly ConcurrentQueue<QueuedGameCommand> pendingCommands = new();
    private readonly ConcurrentQueue<QueuedTimerStatusProbe> pendingTimerStatusProbes = new();
    private readonly ManualLogSource log;

    internal GameCommandDispatcher(ManualLogSource log)
    {
        this.log = log;
    }

    internal async Task<CommandExecutionResult> ExecuteAsync(string commandId, int minutes)
    {
        if (!IsSupported(commandId))
        {
            return CommandExecutionResult.Failed(commandId, "Unsupported command. Supported commands: pomodoro.set-work-minutes, pomodoro.ui-start, pomodoro.ui-stop.");
        }

        if (commandId == "pomodoro.set-work-minutes" && minutes <= 0)
        {
            return CommandExecutionResult.Failed(commandId, $"{commandId} requires a positive minutes value.");
        }

        var queuedCommand = new QueuedGameCommand(commandId, minutes);
        pendingCommands.Enqueue(queuedCommand);

        var completedTask = await Task.WhenAny(queuedCommand.Completion, Task.Delay(DefaultTimeout));
        if (completedTask != queuedCommand.Completion)
        {
            return CommandExecutionResult.Failed(commandId, "Command timed out before Unity main-thread execution completed.");
        }

        return await queuedCommand.Completion;
    }

    internal async Task<TimerStatusSnapshot> CaptureTimerStatusAsync()
    {
        var probe = new QueuedTimerStatusProbe();
        pendingTimerStatusProbes.Enqueue(probe);

        var completedTask = await Task.WhenAny(probe.Completion, Task.Delay(DefaultTimeout));
        if (completedTask != probe.Completion)
        {
            return new TimerStatusSnapshot(false, string.Empty, 0, 0, 0, 0, 0, 0, "Timer status probe timed out.");
        }

        return await probe.Completion;
    }

    internal void DrainOnUnityThread()
    {
        while (pendingCommands.TryDequeue(out var command))
        {
            command.Complete(ExecuteOnUnityThread(command.CommandId, command.Minutes));
        }

        while (pendingTimerStatusProbes.TryDequeue(out var probe))
        {
            probe.Complete(CaptureTimerStatusOnUnityThread());
        }
    }

    private CommandExecutionResult ExecuteOnUnityThread(string commandId, int minutes)
    {
        try
        {
            var targetType = FindType(PomodoroTimerViewTypeName);
            if (targetType is null)
            {
                return CommandExecutionResult.Failed(commandId, $"Type not loaded: {PomodoroTimerViewTypeName}.");
            }

            var target = FindUnityObject(targetType);
            if (target is null)
            {
                return CommandExecutionResult.Failed(commandId, $"No live {PomodoroTimerViewTypeName} instance was found in the current scene.");
            }

            var typedTarget = WrapAsTargetType(target, targetType);
            if (typedTarget == null)
            {
                return CommandExecutionResult.Failed(commandId, $"Found object could not be wrapped as {PomodoroTimerViewTypeName}.");
            }

            if (commandId is "pomodoro.ui-start" or "pomodoro.ui-stop")
            {
                var uiResult = PressPomodoroButton(commandId, typedTarget, targetType);
                if (uiResult.Success)
                {
                    log.LogInfo($"Executed command {commandId} through Pomodoro UI button press.");
                }

                return uiResult;
            }

            if (commandId == "pomodoro.set-work-minutes")
            {
                var setResult = SetPomodoroWorkMinutes(commandId, typedTarget, targetType, minutes);
                if (setResult.Success)
                {
                    log.LogInfo($"Set Pomodoro work minutes to {minutes} through game settings fields.");
                }

                return setResult;
            }

            return CommandExecutionResult.Failed(commandId, "Unsupported command.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            log.LogError(exception.InnerException);
            return CommandExecutionResult.Failed(commandId, exception.InnerException.Message);
        }
        catch (Exception exception)
        {
            log.LogError(exception);
            return CommandExecutionResult.Failed(commandId, exception.Message);
        }
    }

    private static bool IsSupported(string commandId)
    {
        return commandId is "pomodoro.set-work-minutes" or "pomodoro.ui-start" or "pomodoro.ui-stop";
    }

    private static CommandExecutionResult SetPomodoroWorkMinutes(string commandId, object target, Type targetType, int minutes)
    {
        if (target == null)
        {
            return CommandExecutionResult.Failed(commandId, $"Found object could not be wrapped as {PomodoroTimerViewTypeName}.");
        }

        var currentState = ReadInstanceValue(target, targetType, "CurrentState");
        if (!string.IsNullOrWhiteSpace(currentState) && currentState != "Default")
        {
            return CommandExecutionResult.Failed(commandId, $"Work minutes can only be changed before starting the timer. Current state: {currentState}.");
        }

        SetInstanceInt(target, targetType, "m_workTime", minutes);
        SetText(ReadInstanceObject(target, targetType, "m_workTimeText"), minutes.ToString());

        var settingUi = ReadInstanceObject(target, targetType, "m_uiTimerSetting");
        if (settingUi != null)
        {
            var settingType = settingUi.GetType();
            SetInstanceInt(settingUi, settingType, "m_workTime", minutes);
            SetText(ReadInstanceObject(settingUi, settingType, "m_workTimeText"), minutes.ToString());
            // 让游戏自己的设置保存逻辑处理持久化和后续刷新，避免只改显示文本造成不同步。
            InvokeInstanceVoid(settingUi, settingType, "SaveSettings");
        }

        InvokeInstanceVoid(target, targetType, "RefreshSettings");
        SetInstanceInt(target, targetType, "m_workTime", minutes);
        SetText(ReadInstanceObject(target, targetType, "m_workTimeText"), minutes.ToString());
        InvokeInstanceVoid(target, targetType, "SetTime", minutes, 0d);
        InvokeInstanceVoid(target, targetType, "SavePomodoroSettings");

        return CommandExecutionResult.Completed(commandId, $"Set work minutes to {minutes}.");
    }

    private static CommandExecutionResult PressPomodoroButton(string commandId, object target, Type targetType)
    {
        if (target == null)
        {
            return CommandExecutionResult.Failed(commandId, $"Found object could not be wrapped as {PomodoroTimerViewTypeName}.");
        }

        var button = commandId == "pomodoro.ui-start"
            ? ReadInstanceObject(target, targetType, "m_startBtn")
            : FindFirstInteractableStopButton(target, targetType);

        if (button == null)
        {
            return CommandExecutionResult.Failed(commandId, $"No button found for {commandId}.");
        }

        var pressMethod = button.GetType().GetMethod("Press", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (pressMethod != null)
        {
            pressMethod.Invoke(button, null);
            return CommandExecutionResult.Completed(commandId, "Pressed Unity UI Button.Press().");
        }

        var onClick = ReadInstanceObject(button, button.GetType(), "onClick");
        var invokeMethod = onClick?.GetType().GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (invokeMethod == null)
        {
            return CommandExecutionResult.Failed(commandId, "Button had no Press() or onClick.Invoke() method.");
        }

        invokeMethod.Invoke(onClick, null);
        return CommandExecutionResult.Completed(commandId, "Invoked Unity UI Button.onClick.");
    }

    private static object FindFirstInteractableStopButton(object target, Type targetType)
    {
        var stopButtons = ReadInstanceObject(target, targetType, "m_stopBtns");
        if (stopButtons is System.Collections.IEnumerable enumerable)
        {
            foreach (var button in enumerable)
            {
                if (button != null && IsButtonInteractable(button))
                {
                    return button;
                }
            }
        }

        return ReadInstanceObject(target, targetType, "m_skipBtn");
    }

    private static bool IsButtonInteractable(object button)
    {
        var method = button.GetType().GetMethod("IsInteractable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        return method?.Invoke(button, null) as bool? ?? true;
    }

    private static object WrapAsTargetType(object target, Type targetType)
    {
        if (targetType.IsInstanceOfType(target))
        {
            return target;
        }

        if (target is not Il2CppObjectBase il2CppObject)
        {
            return null;
        }

        return Activator.CreateInstance(targetType, il2CppObject.Pointer);
    }

    private static Type FindType(string typeName)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, false))
            .FirstOrDefault(type => type is not null);
    }

    private static object FindUnityObject(Type targetType)
    {
        var il2CppType = ConvertToIl2CppType(targetType);
        if (il2CppType == null)
        {
            return null;
        }

        return FindObjectWithUnityObjectApi(il2CppType) ?? FindObjectWithResourcesApi(il2CppType);
    }

    private static object FindObjectWithUnityObjectApi(Il2CppSystem.Type targetType)
    {
        var found = UnityEngine.Object.FindFirstObjectByType(targetType, FindObjectsInactive.Include);
        if (found != null)
        {
            return found;
        }

        return UnityEngine.Object.FindObjectOfType(targetType, true);
    }

    private static object FindObjectWithResourcesApi(Il2CppSystem.Type targetType)
    {
        var objects = Resources.FindObjectsOfTypeAll(targetType);
        if (objects == null)
        {
            return null;
        }

        foreach (var candidate in objects)
        {
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static TimerStatusSnapshot CaptureTimerStatusOnUnityThread()
    {
        try
        {
            var pomodoroType = FindType(PomodoroTimerViewTypeName);
            var target = pomodoroType == null ? null : FindUnityObject(pomodoroType);
            var typedTarget = target == null || pomodoroType == null ? null : WrapAsTargetType(target, pomodoroType);
            var timerProviderType = FindType("TimerSqliteProvider");

            return new TimerStatusSnapshot(
                typedTarget != null,
                ReadInstanceValue(typedTarget, pomodoroType, "CurrentState"),
                ReadInstanceInt(typedTarget, pomodoroType, "m_workTime"),
                ReadInstanceInt(typedTarget, pomodoroType, "WorkSec"),
                ReadInstanceInt(typedTarget, pomodoroType, "WorkCount"),
                ReadInstanceDouble(typedTarget, pomodoroType, "m_totalWorkTime"),
                ReadStaticLong(timerProviderType, "GetTotalWorkSec"),
                ReadStaticInt(timerProviderType, "GetTotalWorkRunCount"),
                string.Empty);
        }
        catch (Exception exception)
        {
            return new TimerStatusSnapshot(false, string.Empty, 0, 0, 0, 0, 0, 0, exception.Message);
        }
    }

    private static string ReadInstanceValue(object target, Type targetType, string propertyName)
    {
        if (target == null || targetType == null)
        {
            return string.Empty;
        }

        var property = targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var value = property?.GetValue(target);
        return value == null ? string.Empty : value.ToString();
    }

    private static int ReadInstanceInt(object target, Type targetType, string propertyName)
    {
        return int.TryParse(ReadInstanceValue(target, targetType, propertyName), out var value) ? value : 0;
    }

    private static double ReadInstanceDouble(object target, Type targetType, string propertyName)
    {
        return double.TryParse(ReadInstanceValue(target, targetType, propertyName), out var value) ? value : 0;
    }

    private static object ReadInstanceObject(object target, Type targetType, string propertyName)
    {
        if (target == null || targetType == null)
        {
            return null;
        }

        var property = targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return property?.GetValue(target);
    }

    private static void SetInstanceInt(object target, Type targetType, string propertyName, int value)
    {
        var property = targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property?.CanWrite == true)
        {
            property.SetValue(target, value);
        }
    }

    private static void SetText(object textControl, string value)
    {
        var property = textControl?.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property?.CanWrite == true)
        {
            property.SetValue(textControl, value);
        }
    }

    private static void InvokeInstanceVoid(object target, Type targetType, string methodName, params object[] arguments)
    {
        var parameterTypes = arguments.Select(argument => argument.GetType()).ToArray();
        var method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);
        method?.Invoke(target, arguments);
    }

    private static long ReadStaticLong(Type type, string methodName)
    {
        if (type == null)
        {
            return 0;
        }

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
        var value = method?.Invoke(null, null);
        return value == null ? 0 : Convert.ToInt64(value);
    }

    private static int ReadStaticInt(Type type, string methodName)
    {
        return Convert.ToInt32(ReadStaticLong(type, methodName));
    }

    private static Il2CppSystem.Type ConvertToIl2CppType(Type type)
    {
        var classPointer = Il2CppClassPointerStore.GetNativeClassPointer(type);
        return classPointer == IntPtr.Zero ? null : Il2CppType.TypeFromPointer(classPointer, type.FullName ?? type.Name);
    }

}
