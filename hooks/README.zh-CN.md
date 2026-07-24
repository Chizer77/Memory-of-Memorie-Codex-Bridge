# Memory of Memorie Activity Hooks

这是一个可选集成，用于将 Codex、Claude Code 或 OpenCode 的活动同步到游戏内番茄钟。受支持的编码工具开始处理任务时会启动番茄钟，任务结束时会停止番茄钟。

游戏必须正在运行，并已安装 **Memory of Memorie Codex Bridge** 插件及启用本地 HTTP API。

> [English](README.md) | [项目主页](../README.zh-CN.md)

## 安装

在项目根目录或 Release 解压目录中打开 PowerShell，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\hooks\install-hooks.ps1
```

安装器会检测已安装的 Codex、Claude Code 和 OpenCode 配置。输入平台编号可安装到单个平台；输入 `A` 可安装到全部已检测的平台。

安装器会保留用户已有 hooks，只追加本插件需要的开始和结束事件，不会覆盖无关 hook 定义；再次运行也不会重复添加。安装完成后会显示每个平台的实际安装目录，并等待按 Enter 后关闭。

安装后请重启对应编码工具或新建会话。

## 配置

每个平台都会有独立配置文件：

```text
<平台配置目录>/scripts/memory-of-memorie-bridge/settings.json
```

编辑桥接地址和默认工作时长：

```json
{
  "gameApiUrl": "http://127.0.0.1:29461",
  "workMinutes": 25
}
```

| 配置项 | 说明 |
| --- | --- |
| `gameApiUrl` | 游戏插件 HTTP 地址。它应与游戏插件配置中的 `Http.ListenUrl` 相同，但不需要末尾的 `/`。 |
| `workMinutes` | 每次活动触发番茄钟前设置的工作时长。 |

保存后会在下一次活动事件生效，无需重新安装 hooks。

## 默认位置

| 平台 | 配置目录 |
| --- | --- |
| Codex | `%USERPROFILE%\.codex` |
| Claude Code | `%USERPROFILE%\.claude` |
| OpenCode | `%USERPROFILE%\.config\opencode` |

## 手动安装

将整个 `scripts/memory-of-memorie-bridge` 文件夹复制到目标平台的 `scripts` 目录。随后合并对应平台的集成文件：

| 平台 | 集成来源 |
| --- | --- |
| Codex | `platforms/codex/hooks.json` |
| Claude Code | `platforms/claude/settings.json` |
| OpenCode | 将 `platforms/opencode/plugins/memory-of-memorie-activity-bridge.ts` 复制到 OpenCode 的 `plugins` 目录。 |

对于 Codex 和 Claude Code，请将模板中的 `__BRIDGE_ROOT__` 替换为复制后 `memory-of-memorie-bridge` 文件夹的完整路径。合并 hook 条目，不要替换已有配置。

## 注意

- 游戏未启动或游戏 HTTP API 不可用时，编码工具不会被阻塞；本次同步会被跳过。
- 默认 API 地址仅限 `127.0.0.1`。不要将未认证的游戏控制 API 暴露到公网。
