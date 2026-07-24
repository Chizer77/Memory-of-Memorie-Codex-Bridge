using BepInEx;
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
        var probe = new RuntimeProbe();
        commandDispatcher = new GameCommandDispatcher(Log);
        ClassInjector.RegisterTypeInIl2Cpp<UnityCommandRunner>();
        UnityCommandRunner.Configure(commandDispatcher);
        AddComponent<UnityCommandRunner>();

        apiServer = new LocalApiServer(probe, commandDispatcher, Log);
        apiServer.Start();
        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded with Pomodoro UI bridge commands.");
    }
}
