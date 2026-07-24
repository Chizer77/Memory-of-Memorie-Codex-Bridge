using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using BepInEx.Logging;
using MemoryOfMemorieCodexBridge.Api;
using MemoryOfMemorieCodexBridge.Configuration;
using MemoryOfMemorieCodexBridge.Game;
using MemoryOfMemorieCodexBridge.Game.Pomodoro;
using MemoryOfMemorieCodexBridge.Game.Ui;
using MemoryOfMemorieCodexBridge.Game.Music;
using MemoryOfMemorieCodexBridge.Probing;
using MemoryOfMemorieCodexBridge.Wallpaper;

namespace MemoryOfMemorieCodexBridge;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BasePlugin
{
    private LocalApiServer apiServer;
    private HttpApiHotkeyController httpApiHotkey;
    private PomodoroController pomodoro;
    private UnityMainThreadQueue unityMainThreadQueue;
    private MusicHotkeyController musicHotkey;
    private GameUiVisibilityController uiVisibilityController;
    private WallpaperService wallpaperService;

    public override void Load()
    {
        var configuration = BridgeConfigurationStore.LoadOrCreate(Log);
        var probe = new RuntimeProbe();
        uiVisibilityController = new GameUiVisibilityController(
            configuration.Wallpaper.HideGameUi,
            configuration.Wallpaper.TimerEventUiSeconds,
            Log);
        unityMainThreadQueue = new UnityMainThreadQueue();
        pomodoro = new PomodoroController(unityMainThreadQueue, uiVisibilityController, Log);
        if (configuration.Music.Enabled)
        {
            musicHotkey = new MusicHotkeyController(configuration.Music.ToggleHotkey, new MusicController(Log), uiVisibilityController);
        }
        apiServer = new LocalApiServer(probe, pomodoro, Log, configuration.Http.ListenUrl);
        httpApiHotkey = new HttpApiHotkeyController(configuration.Http.ToggleHotkey, apiServer);

        if (configuration.Http.Enabled)
        {
            apiServer.Start();
        }
        else
        {
            Log.LogInfo("Local HTTP API will remain stopped until its configured hotkey is pressed.");
        }

        ClassInjector.RegisterTypeInIl2Cpp<UnityMainThreadHost>();
        UnityMainThreadHost.Configure(unityMainThreadQueue, uiVisibilityController, musicHotkey, httpApiHotkey);
        AddComponent<UnityMainThreadHost>();

        if (configuration.Wallpaper.Enabled)
        {
            wallpaperService = new WallpaperService(configuration.Wallpaper, Log, uiVisibilityController);
            wallpaperService.Start();
        }
        else
        {
            Log.LogInfo("Wallpaper integration is disabled by config.json.");
        }

        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded with Pomodoro, music, and wallpaper integration.");
    }
}
