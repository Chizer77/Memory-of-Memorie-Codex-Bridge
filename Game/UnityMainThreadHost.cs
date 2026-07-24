using UnityEngine;
using MemoryOfMemorieCodexBridge.Api;
using MemoryOfMemorieCodexBridge.Game.Music;
using MemoryOfMemorieCodexBridge.Game.Ui;

namespace MemoryOfMemorieCodexBridge.Game;

public sealed class UnityMainThreadHost : MonoBehaviour
{
    private static UnityMainThreadQueue queue;
    private static GameUiVisibilityController gameUi;
    private static MusicHotkeyController musicHotkey;
    private static HttpApiHotkeyController httpApiHotkey;

    public UnityMainThreadHost(IntPtr pointer) : base(pointer)
    {
    }

    internal static void Configure(UnityMainThreadQueue mainThreadQueue, GameUiVisibilityController gameUiController, MusicHotkeyController musicHotkeyController, HttpApiHotkeyController httpApiHotkeyController)
    {
        queue = mainThreadQueue;
        gameUi = gameUiController;
        musicHotkey = musicHotkeyController;
        httpApiHotkey = httpApiHotkeyController;
    }

    private void Update()
    {
        // IL2CPP 对象仅可由 Unity 主线程访问。
        queue?.Drain();
        gameUi?.DrainOnUnityThread();
        musicHotkey?.UpdateOnUnityThread();
        httpApiHotkey?.UpdateOnUnityThread();
    }
}
