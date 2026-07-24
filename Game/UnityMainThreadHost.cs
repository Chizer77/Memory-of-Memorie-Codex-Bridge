using UnityEngine;
using MemoryOfMemorieCodexBridge.Game.Music;
using MemoryOfMemorieCodexBridge.Game.Ui;

namespace MemoryOfMemorieCodexBridge.Game;

public sealed class UnityMainThreadHost : MonoBehaviour
{
    private static UnityMainThreadQueue queue;
    private static GameUiVisibilityController gameUi;
    private static MusicHotkeyController musicHotkey;

    public UnityMainThreadHost(IntPtr pointer) : base(pointer)
    {
    }

    internal static void Configure(UnityMainThreadQueue mainThreadQueue, GameUiVisibilityController gameUiController, MusicHotkeyController musicHotkeyController)
    {
        queue = mainThreadQueue;
        gameUi = gameUiController;
        musicHotkey = musicHotkeyController;
    }

    private void Update()
    {
        // IL2CPP 对象仅可由 Unity 主线程访问。
        queue?.Drain();
        gameUi?.DrainOnUnityThread();
        musicHotkey?.UpdateOnUnityThread();
    }
}
