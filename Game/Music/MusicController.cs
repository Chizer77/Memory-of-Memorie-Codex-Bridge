using System.Reflection;
using BepInEx.Logging;

namespace MemoryOfMemorieCodexBridge.Game.Music;

internal sealed class MusicController
{
    private const string MusicPlayerUiTypeName = "Framework.GameFlow.UI.MusicPlayerUI";
    private readonly ManualLogSource log;

    internal MusicController(ManualLogSource log)
    {
        this.log = log;
    }

    internal void TogglePlaybackOnUnityThread()
    {
        try
        {
            var musicType = Il2CppReflection.FindType(MusicPlayerUiTypeName);
            if (musicType is null)
            {
                log.LogWarning($"Music toggle skipped because {MusicPlayerUiTypeName} is not loaded.");
                return;
            }

            var found = Il2CppReflection.FindUnityObject(musicType);
            var player = found is null ? null : Il2CppReflection.WrapAsTargetType(found, musicType);
            if (player is null)
            {
                log.LogWarning("Music toggle skipped because no live music player UI was found.");
                return;
            }

            var button = Il2CppReflection.ReadInstanceProperty(player, musicType, "m_playBtn");
            var press = button?.GetType().GetMethod("Press", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (press is null)
            {
                log.LogWarning("Music toggle skipped because the game's play button cannot be pressed.");
                return;
            }

            // 复用游戏播放按钮，确保图标、播放进度和内部状态同步。
            press.Invoke(button, null);
            var isPlaying = Il2CppReflection.ReadInstanceProperty(player, musicType, "m_isPlaying") as bool?;
            log.LogInfo($"Music playback toggled{(isPlaying.HasValue ? $"; playing: {isPlaying.Value}." : ".")}");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            log.LogError(exception.InnerException);
        }
        catch (Exception exception)
        {
            log.LogError(exception);
        }
    }
}
