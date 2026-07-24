using UnityEngine;

namespace MemoryOfMemorieCodexBridge.Commands;

public sealed class UnityCommandRunner : MonoBehaviour
{
    private static GameCommandDispatcher dispatcher;

    public UnityCommandRunner(IntPtr pointer) : base(pointer)
    {
    }

    internal static void Configure(GameCommandDispatcher commandDispatcher)
    {
        dispatcher = commandDispatcher;
    }

    private void Update()
    {
        // Unity 对象调用集中在主线程，避免 HTTP 后台线程触碰游戏状态。
        dispatcher?.DrainOnUnityThread();
    }
}
