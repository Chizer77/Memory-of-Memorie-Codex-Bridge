using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using BepInEx.Logging;
using MemoryOfMemorieCodexBridge.Windows;

namespace MemoryOfMemorieCodexBridge.Configuration;

internal sealed class BridgeConfiguration
{
    public HttpConfiguration Http { get; set; } = new();
    public MusicConfiguration Music { get; set; } = new();
    public WallpaperConfiguration Wallpaper { get; set; } = new();
}

internal sealed class HttpConfiguration
{
    public bool Enabled { get; set; } = true;
    public string ListenUrl { get; set; } = "http://127.0.0.1:29461/";
}

internal sealed class WallpaperConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool CompensateRemovedWindowFrame { get; set; } = true;
    public int ExtraOverscanPixels { get; set; }
    public bool HideGameUi { get; set; } = true;
    public int TimerEventUiSeconds { get; set; } = 3;
    public bool AutoSetWallpaper { get; set; } = true;
    public string ExitWallpaperHotkey { get; set; } = "Ctrl+F10";
    public int AutoReturnSeconds { get; set; }
    public string ReturnWallpaperHotkey { get; set; } = "Ctrl+F12";
}

internal sealed class MusicConfiguration
{
    public bool Enabled { get; set; } = true;
    public string ToggleHotkey { get; set; } = "Ctrl+F11";
}

internal static class BridgeConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static BridgeConfiguration LoadOrCreate(ManualLogSource log)
    {
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "config.json");
        if (!File.Exists(path))
        {
            var created = new BridgeConfiguration();
            Save(path, created);
            log.LogInfo($"Created bridge configuration: {path}");
            return created;
        }

        try
        {
            var configuration = JsonSerializer.Deserialize<BridgeConfiguration>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException("Configuration is empty.");
            Validate(configuration);
            return configuration;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or ArgumentException)
        {
            log.LogError($"Cannot load bridge configuration '{path}': {exception.Message}");
            throw;
        }
    }

    private static void Save(string path, BridgeConfiguration configuration)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(configuration, JsonOptions));
    }

    private static void Validate(BridgeConfiguration configuration)
    {
        if (configuration.Http.Enabled && (!Uri.TryCreate(configuration.Http.ListenUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
        {
            throw new InvalidOperationException("Http.ListenUrl must be an HTTP root URL without a path, query, or fragment.");
        }

        if (configuration.Music.Enabled)
        {
            var musicHotkey = Hotkey.Parse(configuration.Music.ToggleHotkey);
            if (configuration.Wallpaper.Enabled && (musicHotkey.Equals(Hotkey.Parse(configuration.Wallpaper.ExitWallpaperHotkey)) || musicHotkey.Equals(Hotkey.Parse(configuration.Wallpaper.ReturnWallpaperHotkey))))
            {
                throw new InvalidOperationException("Music.ToggleHotkey must not match a wallpaper hotkey.");
            }
        }

        if (!configuration.Wallpaper.Enabled) return;

        if (configuration.Wallpaper.ExtraOverscanPixels is < 0 or > 400)
        {
            throw new InvalidOperationException("Wallpaper.ExtraOverscanPixels must be between 0 and 400.");
        }

        if (configuration.Wallpaper.TimerEventUiSeconds is < 0 or > 60)
        {
            throw new InvalidOperationException("Wallpaper.TimerEventUiSeconds must be between 0 and 60.");
        }

        if (configuration.Wallpaper.AutoReturnSeconds is < 0 or > 3600)
        {
            throw new InvalidOperationException("Wallpaper.AutoReturnSeconds must be between 0 and 3600.");
        }

        var exitHotkey = Hotkey.Parse(configuration.Wallpaper.ExitWallpaperHotkey);
        var returnHotkey = Hotkey.Parse(configuration.Wallpaper.ReturnWallpaperHotkey);
        if (exitHotkey.Equals(returnHotkey))
        {
            throw new InvalidOperationException("Wallpaper hotkeys must not be identical.");
        }
    }
}
