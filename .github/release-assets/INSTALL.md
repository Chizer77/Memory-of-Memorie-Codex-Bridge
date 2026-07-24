# Memory of Memorie Codex Bridge

## Game Plugin

1. Extract this archive into the Memory of Memorie game directory.
2. Start the game. The included BepInEx 6 IL2CPP runtime will generate game interop files on its first launch.
3. Edit `BepInEx/plugins/MemoryOfMemorieCodexBridge/config.json` before starting the game if you need a different HTTP address or shortcut.

The package includes the full BepInEx runtime but no game files. Its BepInEx console is disabled by default and logs are still written to `BepInEx/LogOutput.log`. For an existing BepInEx installation with customized settings, copy only `BepInEx/plugins/MemoryOfMemorieCodexBridge/` instead of overwriting the runtime configuration.

## Codex, Claude, or OpenCode Hooks

The `hooks/` directory is independent from the game plugin. Read `hooks/README.md`, then run `hooks/install-hooks.ps1` in PowerShell. The installer asks which supported platform to configure and preserves existing user hook entries.
