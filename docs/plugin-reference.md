# Memory of Memorie Codex Bridge

`Memory of Memorie Codex Bridge` is a BepInEx 6 IL2CPP plugin for the Windows version of *Memory of Memorie*.

The current milestone exports a local Pomodoro bridge API and enables the UI-equivalent timer flow that was verified to trigger animation and settlement. The current game uses Unity 6000.3 and IL2CPP metadata v39.

## Configuration

After the plugin first starts, it creates `config.json` next to `MemoryOfMemorieCodexBridge.dll`:

The repository also includes the same template as `config.example.json`.

```json
{
  "Http": {
    "Enabled": true,
    "ListenUrl": "http://127.0.0.1:29461/",
    "ToggleHotkey": "Ctrl+F10"
  },
  "Music": {
    "Enabled": true,
    "ToggleHotkey": "Ctrl+F11"
  },
  "Wallpaper": {
    "Enabled": true,
    "CompensateRemovedWindowFrame": true,
    "ExtraOverscanPixels": 0,
    "HideGameUi": true,
    "TimerEventUiSeconds": 3,
    "AutoSetWallpaper": true,
    "ToggleWallpaperHotkey": "Ctrl+F12",
    "AutoReturnSeconds": 0
  }
}
```

- `Http.Enabled` controls whether the local Codex bridge API starts with the game. It can still be started later with `Http.ToggleHotkey`.
- Change `Http.ListenUrl` to use another HTTP port or host. It must be an HTTP root URL without a path, query, or fragment. Keep a loopback host unless you intentionally expose the unauthenticated game-control API to your network.
- Press `Http.ToggleHotkey` to stop or start the local API without changing the game's Pomodoro timer. The default is `Ctrl+F10`.
- Press `Music.ToggleHotkey` to trigger the game's own music play/pause button. Set `Music.Enabled` to `false` to disable this global shortcut. Every configured shortcut must be distinct.
- Set `Wallpaper.Enabled` to `false` to disable wallpaper integration entirely.
- `CompensateRemovedWindowFrame` measures the original title bar and borders, then expands the hosted game window to cover the area they previously occupied.
- Use `ExtraOverscanPixels` from `0` to `400` only when a game keeps additional render padding after its frame is removed.
- Set `HideGameUi` to `true` to hide the main game UI while the window is wallpapered. `TimerEventUiSeconds` controls how long the UI appears after a Pomodoro or music command before it hides again; `0` uses a 200ms interaction-safe minimum.
- `ToggleWallpaperHotkey` is a configurable global shortcut. Each press switches between the normal game window and desktop wallpaper mode.
- Set `Wallpaper.AutoSetWallpaper` to `false` to keep the game as a normal window until `ToggleWallpaperHotkey` is pressed.
- `AutoReturnSeconds` is `0` for no automatic return after leaving wallpaper mode.

Set the same URL in the hooks bridge `settings.json` as `gameApiUrl`.

## Wallpaper Mode

Wallpaper mode waits up to 60 seconds for the current game process's largest visible top-level window, then attaches it to the Explorer WorkerW desktop layer. The game window is removed from the taskbar while wallpapered and restored to a normal taskbar window when detached. `ToggleWallpaperHotkey` switches between normal window mode and wallpaper mode.

## Internal Structure

- `Api/` owns the HTTP contract and does not call Unity or IL2CPP APIs directly.
- `Game/UnityMainThreadQueue` serializes work from HTTP requests onto the Unity main thread.
- `Game/Pomodoro/` owns the native Pomodoro UI adapter, command results, and live status snapshots.
- `Game/Ui/` owns main game UI visibility for wallpaper and Pomodoro events.
- `Game/Music/` owns native music play/pause and its global shortcut handling.
- `Wallpaper/` owns WorkerW hosting and only requests UI visibility changes through `Game/Ui/`.
- `Windows/` owns shared Win32 declarations and global hotkey parsing.

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/health` | `GET` | Plugin and API status |
| `/v1/capabilities` | `GET` | Supported bridge actions and whether their source type/method exists |
| `/v1/probe` | `GET` | Full runtime type and method probe |
| `/v1/timer-status` | `GET` | Read live Pomodoro state, configured work minutes, and stored timer totals |
| `/v1/commands` | `POST` | Execute a supported Pomodoro bridge command on the Unity main thread |

Example:

```powershell
Invoke-RestMethod http://127.0.0.1:29461/v1/capabilities
```

UI-equivalent flow:

```powershell
Invoke-RestMethod http://127.0.0.1:29461/v1/commands -Method Post -ContentType "application/json" -Body '{"id":"pomodoro.set-work-minutes","minutes":25}'
Invoke-RestMethod http://127.0.0.1:29461/v1/commands -Method Post -ContentType "application/json" -Body '{"id":"pomodoro.ui-start"}'
Invoke-RestMethod http://127.0.0.1:29461/v1/commands -Method Post -ContentType "application/json" -Body '{"id":"pomodoro.ui-stop"}'
```

Set the Pomodoro work countdown before starting:

```powershell
Invoke-RestMethod http://127.0.0.1:29461/v1/commands -Method Post -ContentType "application/json" -Body '{"id":"pomodoro.set-work-minutes","minutes":5}'
Invoke-RestMethod http://127.0.0.1:29461/v1/timer-status
```

## Supported Game Actions

These entries are supported by the plugin. The start and stop commands press the same Unity UI buttons used by the game, which keeps animation, timer state, and settlement in sync:

- `pomodoro.ui-start` -> `PomodoroTimerView.m_startBtn.Press()` executable, preferred
- `pomodoro.ui-stop` -> first interactable `PomodoroTimerView.m_stopBtns[].Press()` executable, preferred
- `pomodoro.set-work-minutes` -> `TimerSettingUI.SaveSettings()` plus `PomodoroTimerView.RefreshSettings()` executable before start

Low-level methods such as `WorkTask()`, `EndTimer()`, `TimerTask(int)`, `RestTask()`, and `PauseTimer()` were found in metadata but are intentionally not exposed because direct calls did not follow the full UI/settlement behavior during testing.
