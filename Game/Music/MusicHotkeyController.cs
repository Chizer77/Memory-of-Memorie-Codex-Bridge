using MemoryOfMemorieCodexBridge.Windows;
using MemoryOfMemorieCodexBridge.Game.Ui;

namespace MemoryOfMemorieCodexBridge.Game.Music;

internal sealed class MusicHotkeyController
{
    private readonly Hotkey toggleHotkey;
    private readonly MusicController music;
    private readonly GameUiVisibilityController gameUi;
    private bool toggleWasPressed;
    private DateTime? toggleAfter;

    internal MusicHotkeyController(string toggleHotkey, MusicController music, GameUiVisibilityController gameUi)
    {
        this.toggleHotkey = Hotkey.Parse(toggleHotkey);
        this.music = music;
        this.gameUi = gameUi;
    }

    internal void UpdateOnUnityThread()
    {
        if (toggleAfter.HasValue && DateTime.UtcNow >= toggleAfter.Value)
        {
            music.TogglePlaybackOnUnityThread();
            toggleAfter = null;
        }

        var isPressed = toggleHotkey.IsPressed();
        if (isPressed && !toggleWasPressed && !toggleAfter.HasValue)
        {
            // 隐藏状态的按钮不可交互，先恢复游戏 UI 再走原生按钮流程。
            gameUi.ShowForTransientEvent();
            toggleAfter = DateTime.UtcNow.AddMilliseconds(150);
        }

        toggleWasPressed = isPressed;
    }
}
