import { spawn } from "node:child_process"

const scriptPath = new URL("../scripts/memory-of-memorie-bridge/memory-of-memorie-agent-hook.ps1", import.meta.url).pathname
let isWorking = false

function notifyGame(event: "start" | "stop") {
  // 异步启动，避免游戏接口暂时不可用时阻塞 OpenCode 的事件循环。
  const child = spawn("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-Event", event], {
    detached: true,
    stdio: "ignore",
    windowsHide: true,
  })
  child.unref()
}

export const MemoryOfMemorieActivityBridge = async () => ({
  event: async ({ event }: any) => {
    if (event.type !== "session.status") return
    const status = event.properties?.status?.type
    if (status === "idle") {
      if (!isWorking) return
      isWorking = false
      notifyGame("stop")
      return
    }
    if (!status || isWorking) return
    isWorking = true
    notifyGame("start")
  },
})
