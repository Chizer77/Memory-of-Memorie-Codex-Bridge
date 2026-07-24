# Memory of Memorie Activity Hooks

This optional integration synchronizes Codex, Claude Code, or OpenCode activity with the in-game Pomodoro timer. When a supported coding tool begins work, it starts a Pomodoro session; when work completes, it stops the session.

The game must be running with the **Memory of Memorie Codex Bridge** plugin installed and its local HTTP API available.

> [中文说明](README.zh-CN.md) | [Main project README](../README.md)

## Install

Open PowerShell in the project root or extracted Release directory and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\hooks\install-hooks.ps1
```

The installer detects installed Codex, Claude Code, and OpenCode configurations. Enter a platform number to install one integration, or enter `A` to install every detected platform.

The installer preserves existing user hooks. It appends this bridge's start and stop entries without overwriting unrelated hook definitions, and it does not add duplicate entries when run again. It displays every installed location and waits for Enter before closing.

Restart the selected coding tool or start a new session after installation.

## Configure

Each selected platform receives an independent configuration file:

```text
<platform configuration directory>/scripts/memory-of-memorie-bridge/settings.json
```

Edit the bridge URL and default work duration:

```json
{
  "gameApiUrl": "http://127.0.0.1:29461",
  "workMinutes": 25
}
```

| Setting | Description |
| --- | --- |
| `gameApiUrl` | The game plugin's HTTP address. It must match `Http.ListenUrl` in the game plugin configuration, without the trailing slash requirement. |
| `workMinutes` | The Pomodoro work duration set before each activity-triggered session. |

Saving this file takes effect on the next activity event; reinstalling hooks is unnecessary.

## Default Locations

| Platform | Configuration directory |
| --- | --- |
| Codex | `%USERPROFILE%\.codex` |
| Claude Code | `%USERPROFILE%\.claude` |
| OpenCode | `%USERPROFILE%\.config\opencode` |

## Manual Installation

Copy the entire `scripts/memory-of-memorie-bridge` folder into the target platform's `scripts` directory. Then merge the matching platform integration:

| Platform | Source integration |
| --- | --- |
| Codex | `platforms/codex/hooks.json` |
| Claude Code | `platforms/claude/settings.json` |
| OpenCode | Copy `platforms/opencode/plugins/memory-of-memorie-activity-bridge.ts` into the OpenCode `plugins` directory. |

For Codex and Claude Code, replace `__BRIDGE_ROOT__` in the template with the full path to the copied `memory-of-memorie-bridge` folder. Merge hook entries rather than replacing existing configuration.

## Notes

- If the game is not running or its HTTP API is unavailable, the coding tool is not blocked; the activity sync is skipped.
- The default API address uses `127.0.0.1`. Do not expose the unauthenticated game-control API to the public internet.
