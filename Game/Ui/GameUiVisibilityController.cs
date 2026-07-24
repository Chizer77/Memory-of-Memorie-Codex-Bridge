using System.Reflection;
using BepInEx.Logging;

namespace MemoryOfMemorieCodexBridge.Game.Ui;

internal sealed class GameUiVisibilityController
{
    private const string MainSceneViewTypeName = "App.Main.View.MainSceneView";
    private readonly object stateGate = new();
    private readonly ManualLogSource log;
    private readonly bool enabled;
    private readonly TimeSpan timerEventDuration;
    private bool wallpaperMode;
    private bool desiredVisible = true;
    private bool hasAppliedVisibility;
    private bool appliedVisible;
    private DateTime? hideAfter;
    private DateTime nextApplyAttempt = DateTime.MinValue;

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
        }
    }

    internal void ShowForTransientEvent()
    {
        if (!enabled || timerEventDuration <= TimeSpan.Zero) return;
        lock (stateGate)
        {
            if (!wallpaperMode) return;
            desiredVisible = true;
            hideAfter = DateTime.UtcNow.Add(timerEventDuration);
        }
    }

    internal void DrainOnUnityThread()
    {
        if (!enabled) return;

        bool visible;
        lock (stateGate)
        {
            if (wallpaperMode && hideAfter.HasValue && DateTime.UtcNow >= hideAfter.Value)
            {
                desiredVisible = false;
                hideAfter = null;
            }
            visible = desiredVisible;
        }

        if (hasAppliedVisibility && appliedVisible == visible) return;
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

        appliedVisible = visible;
        hasAppliedVisibility = true;
        log.LogInfo($"Game UI {(visible ? "shown" : "hidden")} for wallpaper mode.");
    }

    private static bool TrySetVisibility(bool visible)
    {
        var type = Il2CppReflection.FindType(MainSceneViewTypeName);
        if (type is null) return false;

        var target = Il2CppReflection.FindUnityObject(type);
        if (target is null) return false;

        var typedTarget = Il2CppReflection.WrapAsTargetType(target, type);
        if (typedTarget is null) return false;

        var methodName = visible ? "UIInTask" : "UIOutTask";
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
        if (method is null) return false;

        // 保留游戏自己的 UI 过渡动画，避免壁纸画面突变。
        method.Invoke(typedTarget, new object[] { false });
        return true;
    }
}
