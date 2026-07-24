using BepInEx.Logging;
using MemoryOfMemorieCodexBridge.Game.Ui;
using MemoryOfMemorieCodexBridge.Configuration;
using MemoryOfMemorieCodexBridge.Windows;

namespace MemoryOfMemorieCodexBridge.Wallpaper;

internal sealed class WallpaperService
{
    private readonly WallpaperConfiguration configuration;
    private readonly ManualLogSource log;
    private readonly GameUiVisibilityController uiVisibilityController;

    internal WallpaperService(WallpaperConfiguration configuration, ManualLogSource log, GameUiVisibilityController uiVisibilityController)
    {
        this.configuration = configuration;
        this.log = log;
        this.uiVisibilityController = uiVisibilityController;
    }

    internal void Start()
    {
        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        var target = await WindowFinder.FindCurrentProcessWindowAsync(TimeSpan.FromSeconds(60));
        if (target == 0)
        {
            log.LogWarning("Wallpaper mode skipped because the game window was not found within 60 seconds.");
            return;
        }

        var exitHotkey = Hotkey.Parse(configuration.ExitWallpaperHotkey);
        var returnHotkey = Hotkey.Parse(configuration.ReturnWallpaperHotkey);
        var wallpaper = new WallpaperController(
            target,
            configuration.CompensateRemovedWindowFrame,
            configuration.ExtraOverscanPixels,
            log.LogInfo,
            log.LogWarning);
        var isWallpaper = false;
        DateTime? detachedAt = null;
        var lastRecoveryAttempt = DateTime.MinValue;
        var exitWasPressed = false;
        var returnWasPressed = false;

        try
        {
            if (configuration.AutoSetWallpaper)
            {
                if (!wallpaper.TryAttach(out var error))
                {
                    log.LogWarning($"Wallpaper mode skipped: {error}");
                    return;
                }
                isWallpaper = true;
                uiVisibilityController.SetWallpaperMode(true);
            }

            while (NativeMethods.IsWindow(target))
            {
                var exitIsPressed = exitHotkey.IsPressed();
                var returnIsPressed = returnHotkey.IsPressed();

                if (exitIsPressed && !exitWasPressed && isWallpaper)
                {
                    wallpaper.Detach(true);
                    isWallpaper = false;
                    uiVisibilityController.SetWallpaperMode(false);
                    detachedAt = DateTime.UtcNow;
                }
                else if (returnIsPressed && !returnWasPressed && !isWallpaper && wallpaper.TryAttach(out var error))
                {
                    isWallpaper = true;
                    uiVisibilityController.SetWallpaperMode(true);
                    detachedAt = null;
                }

                if (isWallpaper && !wallpaper.Maintain())
                {
                    wallpaper.Detach(false);
                    isWallpaper = false;
                    uiVisibilityController.SetWallpaperMode(false);
                    lastRecoveryAttempt = DateTime.UtcNow;
                }

                if (!isWallpaper && detachedAt.HasValue && configuration.AutoReturnSeconds > 0 && (DateTime.UtcNow - detachedAt.Value).TotalSeconds >= configuration.AutoReturnSeconds)
                {
                    detachedAt = null;
                    lastRecoveryAttempt = DateTime.UtcNow;
                }

                if (!isWallpaper && !detachedAt.HasValue && (DateTime.UtcNow - lastRecoveryAttempt).TotalSeconds >= 1 && configuration.AutoSetWallpaper)
                {
                    lastRecoveryAttempt = DateTime.UtcNow;
                    if (wallpaper.TryAttach(out _))
                    {
                        isWallpaper = true;
                        uiVisibilityController.SetWallpaperMode(true);
                    }
                }

                exitWasPressed = exitIsPressed;
                returnWasPressed = returnIsPressed;
                await Task.Delay(50);
            }
        }
        catch (Exception exception)
        {
            log.LogError(exception);
        }
        finally
        {
            wallpaper.Detach(false);
            uiVisibilityController.SetWallpaperMode(false);
        }
    }
}
