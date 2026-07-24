using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using BepInEx.Logging;
using MemoryOfMemorieCodexBridge.Api;
using MemoryOfMemorieCodexBridge.Commands;
using MemoryOfMemorieCodexBridge.Probing;

namespace MemoryOfMemorieCodexBridge;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BasePlugin
{
    private LocalApiServer apiServer;
    private GameCommandDispatcher commandDispatcher;

    public override void Load()
    {
        var listenUrl = Config.Bind(
            "Local API",
            "ListenUrl",
            "http://127.0.0.1:29461/",
            "HTTP listener root URL. Keep a loopback address unless you intentionally expose the unauthenticated game control API.");
        var probe = new RuntimeProbe();
        commandDispatcher = new GameCommandDispatcher(Log);
        ClassInjector.RegisterTypeInIl2Cpp<UnityCommandRunner>();
        UnityCommandRunner.Configure(commandDispatcher);
        AddComponent<UnityCommandRunner>();

        apiServer = new LocalApiServer(probe, commandDispatcher, Log, listenUrl.Value);
        apiServer.Start();
        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded with Pomodoro UI bridge commands.");
    }
}
