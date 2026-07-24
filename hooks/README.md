# Memory of Memorie Activity Bridge

本工具把 Codex、Claude Code 或 OpenCode 的工作状态同步到游戏番茄钟：开始处理任务时启动番茄钟，任务完成后停止。游戏必须已安装并运行 `Memory of Memorie Codex Bridge` 插件。

## 安装

在本项目根目录打开 PowerShell，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\hooks\install-hooks.ps1
```

安装器会自动检测已安装的 Codex、Claude Code 和 OpenCode，并在终端显示编号。输入平台编号安装到单个平台，输入 `A` 安装到全部已检测的平台。完成后会显示每个平台的实际安装目录，按 Enter 关闭。

安装不会覆盖你已有的 hooks。它只会追加本插件需要的开始和结束事件；再次运行安装器也不会重复添加。

安装完成后，重启对应的编码工具或开始一个新会话，使新 hook 生效。

## 配置

每个平台都会有独立的配置文件：

```text
<平台配置目录>/scripts/memory-of-memorie-bridge/settings.json
```

编辑其中两项：

```json
{
  "gameApiUrl": "http://127.0.0.1:29461",
  "workMinutes": 25
}
```

- `gameApiUrl`：游戏插件的 HTTP 地址。
- `workMinutes`：每次任务开始时设置的番茄钟分钟数。

如果你在游戏插件的 BepInEx 配置中修改了 `ListenUrl`，这里的 `gameApiUrl` 必须改成相同地址。保存后，下次任务自动使用新值，无需重新安装 hooks。

## 默认位置

- Codex：`%USERPROFILE%\.codex`
- Claude Code：`%USERPROFILE%\.claude`
- OpenCode：`%USERPROFILE%\.config\opencode`

## 手动安装

若不使用安装器，将 `scripts/memory-of-memorie-bridge` 整个文件夹复制到目标平台的 `scripts` 目录。然后合并对应平台模板中的 hooks：

- Codex：`platforms/codex/hooks.json`
- Claude Code：`platforms/claude/settings.json`
- OpenCode：将 `platforms/opencode/plugins/memory-of-memorie-activity-bridge.ts` 复制到 OpenCode 的 `plugins` 目录

Codex 和 Claude Code 模板里的 `__BRIDGE_ROOT__` 替换为复制后 `memory-of-memorie-bridge` 文件夹的完整路径。

## 注意

- 游戏未启动或游戏插件不可用时，编码工具不会被阻塞；本次同步会被跳过。
- 默认仅访问本机 `127.0.0.1`。不要把游戏 API 暴露到公网。
