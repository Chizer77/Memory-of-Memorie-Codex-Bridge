using System.Reflection;
using BepInEx.Logging;

namespace MemoryOfMemorieCodexBridge.Game.Ui;

internal sealed class GameUiVisibilityController
{
    private const string MainSceneViewTypeName = "App.Main.View.MainSceneView";
    internal static readonly TimeSpan InteractionRevealDelay = TimeSpan.FromMilliseconds(150);
    private readonly object stateGate = new();
    private readonly ManualLogSource log;
    private readonly bool enabled;
    private readonly TimeSpan timerEventDuration;
    private bool wallpaperMode;
    private bool desiredVisible = true;
    private bool hasAppliedVisibility;
    private bool appliedVisible;
    private DateTime? hideAfter;
    private DateTime? initialWallpaperHideAfter;
    private DateTime nextApplyAttempt = DateTime.MinValue;
    private TaskCompletionSource<bool> pendingInteractionVisibility;

    internal GameUiVisibilityController(bool enabled, int timerEventUiSeconds, ManualLogSource log)
    {
        this.enabled = enabled;
        timerEventDuration = TimeSpan.FromSeconds(timerEventUiSeconds);
        this.log = log;
    }

    internal void SetWallpaperMode(bool active)
    {
        if (!enabled) return;
        lock (stateGate)
        {
            wallpaperMode = active;
            desiredVisible = !active;
            hideAfter = null;

            initialWallpaperHideAfter = null;
        }
    }

    internal void BeginInitialWallpaperMode()
    {
        if (!enabled) return;
        lock (stateGate)
        {
            wallpaperMode = true;
            desiredVisible = false;
            hideAfter = null;

            initialWallpaperHideAfter = DateTime.UtcNow.AddSeconds(15);
        }
    }

    internal Task ShowForInteraction(TimeSpan? minimumVisibleDuration = null)
    {
        if (!enabled) return Task.CompletedTask;
        lock (stateGate)
        {
            if (!wallpaperMode) return Task.CompletedTask;

            // 启动宽限期内游戏 UI 尚未被隐藏，避免首次 Hook 为重复的显示动画等待而超时。
            var uiIsStillVisibleFromStartup = initialWallpaperHideAfter.HasValue;
            desiredVisible = true;
            var visibleDuration = minimumVisibleDuration.GetValueOrDefault();
            if (visibleDuration < GetInteractionVisibilityDuration()) visibleDuration = GetInteractionVisibilityDuration();
            hideAfter = DateTime.UtcNow.Add(visibleDuration);
            initialWallpaperHideAfter = null;
            if (uiIsStillVisibleFromStartup)
            {
                hasAppliedVisibility = true;
                appliedVisible = true;
                return Task.CompletedTask;
            }
            pendingInteractionVisibility ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return pendingInteractionVisibility.Task;
        }
    }

    internal void DrainOnUnityThread()
    {
        if (!enabled) return;

        bool visible;
        bool requiresApply;
        var initialHideIsDue = false;
        lock (stateGate)
        {
            if (initialWallpaperHideAfter.HasValue)
            {
                if (DateTime.UtcNow < initialWallpaperHideAfter.Value) return;

                initialWallpaperHideAfter = null;
                initialHideIsDue = true;
            }
            if (wallpaperMode && hideAfter.HasValue && DateTime.UtcNow >= hideAfter.Value)
            {
                desiredVisible = false;
                hideAfter = null;
            }
            visible = desiredVisible;
            requiresApply = initialHideIsDue
                || !hasAppliedVisibility
                || appliedVisible != visible
                || (visible && pendingInteractionVisibility is not null);
        }

        if (!requiresApply) return;
        if (DateTime.UtcNow < nextApplyAttempt) return;

        nextApplyAttempt = DateTime.UtcNow.AddSeconds(1);
        try
        {
            if (!TrySetVisibility(visible)) return;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            log.LogWarning($"Game UI visibility update failed: {exception.InnerException.Message}");
            return;
        }
        catch (Exception exception)
        {
            log.LogWarning($"Game UI visibility update failed: {exception.Message}");
            return;
        }

        TaskCompletionSource<bool> interactionVisibility = null;
        lock (stateGate)
        {
            appliedVisible = visible;
            hasAppliedVisibility = true;
            if (visible && pendingInteractionVisibility is not null)
            {
                interactionVisibility = pendingInteractionVisibility;
                pendingInteractionVisibility = null;
            }
        }
        interactionVisibility?.TrySetResult(true);
        log.LogInfo($"Game UI {(visible ? "shown" : "hidden")} for wallpaper mode.");
    }

    private static bool TrySetVisibility(bool visible)
    {
        if (!TryFindMainSceneView(out var type, out var target)) return false;

        var methodName = visible ? "UIInTask" : "UIOutTask";
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
        if (method is null) return false;

        method.Invoke(target, new object[] { false });
        return true;
    }

    // 隐藏 UI 时原生按钮不可交互，0 秒配置也必须留下最短操作窗口。
    private TimeSpan GetInteractionVisibilityDuration() => timerEventDuration > TimeSpan.Zero
        ? timerEventDuration
        : TimeSpan.FromMilliseconds(200);

    private static bool TryFindMainSceneView(out Type type, out object typedTarget)
    {
        type = Il2CppReflection.FindType(MainSceneViewTypeName);
        typedTarget = null;
        if (type is null) return false;

        var target = Il2CppReflection.FindUnityObject(type);
        if (target is null) return false;

        typedTarget = Il2CppReflection.WrapAsTargetType(target, type);
        return typedTarget is not null;
    }
}
