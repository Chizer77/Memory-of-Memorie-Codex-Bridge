# Memory of Memorie Codex Bridge

[![游戏](https://img.shields.io/badge/游戏-Memory%20of%20Memorie-e76f51)](https://store.steampowered.com/app/4337440/)
[![BepInEx](https://img.shields.io/badge/BepInEx-6.0.0--be.785-4c9f70)](https://builds.bepinex.dev/projects/bepinex_be/785/)
[![.NET](https://img.shields.io/badge/.NET-net6.0-512bd4)](https://dotnet.microsoft.com/download/dotnet/6.0)
[![平台](https://img.shields.io/badge/平台-Windows%20x64-0078d4)](https://learn.microsoft.com/windows/)
[![许可证](https://img.shields.io/badge/许可证-GPL--3.0--or--later-blue.svg)](LICENSE)

**Memory of Memorie Codex Bridge** 是 *Memory of Memorie* 的 Codex 桌面插件。它提供本地 HTTP 接口、与游戏原生流程同步的番茄钟控制、桌面壁纸模式，以及可安装到 Codex、Claude Code 和 OpenCode 的活动 hooks。

番茄钟启动和结束均通过游戏原本的 UI 按钮流程执行，因此动画、计时状态和结算会保持同步。

> [English](README.md) | [插件参考](docs/plugin-reference.md) | [Hooks 使用说明](hooks/README.zh-CN.md)

## 功能

- 通过本地 API 启动、停止游戏原生番茄钟。
- 使用 Codex、Claude Code 或 OpenCode hooks 同步编码任务活动。
- 将游戏窗口附着到 WorkerW 桌面层，空闲时隐藏游戏 UI。
- 用全局快捷键开关 HTTP、游戏音乐和壁纸模式。
- 默认只监听本机 `127.0.0.1:29461/`。

## 用户安装

### 1. 安装游戏插件

1. 下载最新 Release。压缩包名称包含插件版本、BepInEx 版本、.NET 目标和平台：

   ```text
   Memory-of-Memorie-Codex-Bridge-v0.2.1-BepInEx-6.0.0-be.785-net6.0-win-x64.zip
   ```

2. 退出游戏，将压缩包内的**内容**解压到 *Memory of Memorie* 游戏根目录。
3. 启动游戏一次，BepInEx 会在首次启动时生成 IL2CPP interop 文件。
4. 按需编辑配置文件：

   ```text
   BepInEx/plugins/MemoryOfMemorieCodexBridge/config.json
   ```

若已有自定义 BepInEx 安装，请只复制压缩包内的 `BepInEx/plugins/MemoryOfMemorieCodexBridge/`，避免覆盖已有运行时配置。

### 2. 配置游戏插件

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

| 配置项 | 说明 |
| --- | --- |
| `Http.Enabled` | 是否随游戏启动本地 API；仍可用 `Ctrl+F10` 随时开关。 |
| `Http.ListenUrl` | 本地 API 根地址。除非明确需要局域网访问，否则保留 `127.0.0.1`。 |
| `Http.ToggleHotkey` | 启动或停止 API。默认 `Ctrl+F10`。 |
| `Music.Enabled` / `Music.ToggleHotkey` | 是否启用游戏原生音乐开关。默认 `Ctrl+F11`。 |
| `Wallpaper.Enabled` | 是否启用壁纸模式。 |
| `CompensateRemovedWindowFrame` | 壁纸模式移除窗口框后，补足原标题栏与边框区域。 |
| `ExtraOverscanPixels` | 额外覆盖 `0` 到 `400` 像素，用于消除剩余渲染空隙。 |
| `HideGameUi` | 壁纸模式下隐藏主 UI。 |
| `TimerEventUiSeconds` | 番茄钟或音乐操作后显示 UI 的秒数，范围 `0` 到 `60`。 |
| `AutoSetWallpaper` | 启动游戏时自动进入壁纸模式。 |
| `ToggleWallpaperHotkey` | 普通窗口和壁纸模式之间切换。默认 `Ctrl+F12`。 |
| `AutoReturnSeconds` | 离开壁纸模式后自动返回的秒数；`0` 为不自动返回。 |

所有快捷键必须不同。修改 `config.json` 后请重启游戏。

### 3. 安装编码工具 Hooks（可选）

`hooks/` 与游戏插件独立。安装器会检测 Codex、Claude Code 和 OpenCode，并在保留现有 hooks 的前提下追加本项目的活动同步配置。

在 Release 解压目录根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\hooks\install-hooks.ps1
```

按提示选择目标平台，随后重启对应编码工具或新建会话。每个平台都有独立配置：

```text
<平台配置目录>/scripts/memory-of-memorie-bridge/settings.json
```

将其中的 `gameApiUrl` 设为与游戏 `Http.ListenUrl` 相同的地址，并按需设置 `workMinutes`。手动安装方式和平台目录见 [Hooks 使用说明](hooks/README.zh-CN.md)。

## 本地 API

HTTP 桥接运行时可使用以下接口：

| 接口 | 方法 | 用途 |
| --- | --- | --- |
| `/health` | `GET` | 检查插件与桥接可用性。 |
| `/v1/timer-status` | `GET` | 读取游戏原生番茄钟实时状态。 |
| `/v1/commands` | `POST` | 设置分钟数、启动或停止游戏原生番茄钟。 |

```powershell
Invoke-RestMethod http://127.0.0.1:29461/v1/commands -Method Post -ContentType 'application/json' -Body '{"id":"pomodoro.ui-start"}'
```

完整接口约定与已验证的游戏集成行为见 [插件参考](docs/plugin-reference.md)。

## 开发

开发需要 Windows x64、.NET 8 SDK，以及 BepInEx `6.0.0-be.785` IL2CPP 运行时。设置运行时目录后构建：

```powershell
$env:MEMORY_OF_MEMORIE_BEPINEX_DIR = '<BepInEx runtime directory>\BepInEx'
dotnet build -c Release
```

`.github/build-references` 已保存该游戏需要的 IL2CPP 编译引用，因此编译本身不需要安装游戏。

## 致谢与许可

- [BepInEx](https://github.com/BepInEx/BepInEx) `6.0.0-be.785+6abdba4`，LGPL-2.1。
- [AppToWallpaper](https://github.com/ixuan789/AppToWallpaper)，提交 `6c181f1`，其 WorkerW 壁纸集成遵循 GPL-3.0-or-later。

本项目使用 [GPL-3.0-or-later](LICENSE) 许可。
