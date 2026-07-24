# Memory of Memorie Codex Bridge

[![Game](https://img.shields.io/badge/Game-Memory%20of%20Memorie-e76f51)](https://store.steampowered.com/app/4337440/)
[![BepInEx](https://img.shields.io/badge/BepInEx-6.0.0--be.785-4c9f70)](https://builds.bepinex.dev/projects/bepinex_be/785/)
[![.NET](https://img.shields.io/badge/.NET-net6.0-512bd4)](https://dotnet.microsoft.com/download/dotnet/6.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d4)](https://learn.microsoft.com/windows/)
[![License](https://img.shields.io/badge/License-GPL--3.0--or--later-blue.svg)](LICENSE)

**Memory of Memorie Codex Bridge** is a Codex desktop plugin for *Memory of Memorie*. It provides a local HTTP bridge, native Pomodoro controls, wallpaper mode, and optional activity hooks for Codex, Claude Code, and OpenCode.

Pomodoro commands use the game's own UI button flow, keeping start, stop, animation, and settlement aligned with the game.

> [中文说明](README.zh-CN.md) | [Plugin reference](docs/plugin-reference.md) | [Hooks guide](hooks/README.md)

## Features

- Start and stop the native in-game Pomodoro flow through a local API.
- Synchronize coding activity from Codex, Claude Code, or OpenCode through optional hooks.
- Attach the game window to the WorkerW desktop layer and hide its UI while idle.
- Toggle the local HTTP API, game music, and wallpaper mode with global shortcuts.
- Keep external control on loopback by default: `http://127.0.0.1:29461/`.

## User Installation

### 1. Install the Game Plugin

1. Download the latest Release archive. Its name identifies the plugin version, bundled BepInEx version, target runtime, and platform:

   ```text
   Memory-of-Memorie-Codex-Bridge-v0.2.1-BepInEx-6.0.0-be.785-net6.0-win-x64.zip
   ```

2. Exit the game, then extract the archive **contents** into the *Memory of Memorie* game directory.
3. Start the game once. BepInEx creates IL2CPP interop files on first launch.
4. Edit the generated configuration when needed:

   ```text
   BepInEx/plugins/MemoryOfMemorieCodexBridge/config.json
   ```

If you already use BepInEx with custom settings, copy only `BepInEx/plugins/MemoryOfMemorieCodexBridge/` from the archive instead of replacing the runtime files.

### 2. Configure the Game Plugin

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

| Setting | Description |
| --- | --- |
| `Http.Enabled` | Start the local API with the game. `Ctrl+F10` can always start or stop it later. |
| `Http.ListenUrl` | Local API root URL. Keep `127.0.0.1` unless you intentionally allow network access. |
| `Http.ToggleHotkey` | Start or stop the API. Default: `Ctrl+F10`. |
| `Music.Enabled` / `Music.ToggleHotkey` | Enable the global native music toggle. Default: `Ctrl+F11`. |
| `Wallpaper.Enabled` | Enable wallpaper integration. |
| `CompensateRemovedWindowFrame` | Expand the hosted window to cover the title-bar area removed in wallpaper mode. |
| `ExtraOverscanPixels` | Add `0` to `400` pixels of extra coverage for games with remaining render gaps. |
| `HideGameUi` | Hide the main UI while wallpapered. |
| `TimerEventUiSeconds` | Show UI for `0` to `60` seconds after a Pomodoro or music action. |
| `AutoSetWallpaper` | Attach to the desktop automatically at startup. |
| `ToggleWallpaperHotkey` | Switch between normal window and wallpaper mode. Default: `Ctrl+F12`. |
| `AutoReturnSeconds` | Reattach automatically after leaving wallpaper mode. `0` disables automatic return. |

All configured shortcuts must be distinct. Restart the game after editing `config.json`.

### 3. Install Activity Hooks (Optional)

The `hooks/` folder is independent of the game plugin. It installs activity integration for Codex, Claude Code, or OpenCode while preserving existing user hooks.

From the extracted archive root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\hooks\install-hooks.ps1
```

The installer detects supported platforms and asks where to install. Then restart the selected coding tool or start a new session.

Each platform receives an editable settings file:

```text
<platform configuration directory>/scripts/memory-of-memorie-bridge/settings.json
```

Set `gameApiUrl` to the same address as `Http.ListenUrl` and set the desired `workMinutes`. See the [hooks guide](hooks/README.md) for manual installation and platform locations.

## Local API

The API is available only while the HTTP bridge is running.

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/health` | `GET` | Check plugin and bridge availability. |
| `/v1/timer-status` | `GET` | Read the live native Pomodoro state. |
| `/v1/commands` | `POST` | Set work minutes, start, or stop the native Pomodoro flow. |

```powershell
Invoke-RestMethod http://127.0.0.1:29461/v1/commands -Method Post -ContentType 'application/json' -Body '{"id":"pomodoro.ui-start"}'
```

See the [plugin reference](docs/plugin-reference.md) for the complete endpoint contract and verified game integration behavior.

## Development

Development requires Windows x64, the .NET 8 SDK, and the BepInEx `6.0.0-be.785` IL2CPP runtime. Set the runtime directory, then build:

```powershell
$env:MEMORY_OF_MEMORIE_BEPINEX_DIR = '<BepInEx runtime directory>\BepInEx'
dotnet build -c Release
```

The target game's generated IL2CPP compile references are versioned under `.github/build-references`; a local game installation is not required to compile.

## Credits and License

- [BepInEx](https://github.com/BepInEx/BepInEx) `6.0.0-be.785+6abdba4`, distributed under LGPL-2.1.
- [AppToWallpaper](https://github.com/ixuan789/AppToWallpaper), commit `6c181f1`, adapted for WorkerW wallpaper integration under GPL-3.0-or-later.

This project is licensed under [GPL-3.0-or-later](LICENSE).
