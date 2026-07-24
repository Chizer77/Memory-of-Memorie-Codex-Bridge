using MemoryOfMemorieCodexBridge.Windows;

namespace MemoryOfMemorieCodexBridge.Api;

internal sealed class HttpApiHotkeyController
{
    private readonly LocalApiServer server;
    private readonly Hotkey toggleHotkey;
    private bool toggleWasPressed;

    internal HttpApiHotkeyController(string toggleHotkey, LocalApiServer server)
    {
        this.server = server;
        this.toggleHotkey = Hotkey.Parse(toggleHotkey);
    }

    internal void UpdateOnUnityThread()
    {
        var isPressed = toggleHotkey.IsPressed();
        if (isPressed && !toggleWasPressed)
        {
            if (server.IsListening) server.Stop();
            else server.Start();
        }

        toggleWasPressed = isPressed;
    }
}
