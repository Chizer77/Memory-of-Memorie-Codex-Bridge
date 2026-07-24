using System.Diagnostics;
using System.Runtime.InteropServices;
using MemoryOfMemorieCodexBridge.Windows;

namespace MemoryOfMemorieCodexBridge.Wallpaper;

internal static class WindowFinder
{
    internal static async Task<nint> FindCurrentProcessWindowAsync(TimeSpan timeout)
    {
        using var process = Process.GetCurrentProcess();
        return await FindAsync((uint)process.Id, timeout);
    }

    private static async Task<nint> FindAsync(uint processId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var context = new WindowSearchContext(processId);
            var handle = GCHandle.Alloc(context);
            try { NativeMethods.EnumWindows(EnumWindow, GCHandle.ToIntPtr(handle)); }
            finally { handle.Free(); }
            if (context.BestWindow != 0) return context.BestWindow;

            await Task.Delay(250);
        }
        return 0;
    }

    private static bool EnumWindow(nint window, nint parameter)
    {
        var context = (WindowSearchContext)GCHandle.FromIntPtr(parameter).Target!;
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (context.ProcessId != processId || !NativeMethods.IsWindowVisible(window) || !NativeMethods.GetWindowRect(window, out var rect)) return true;
        var area = (long)Math.Max(0, rect.Right - rect.Left) * Math.Max(0, rect.Bottom - rect.Top);
        if (area > context.BestArea) { context.BestArea = area; context.BestWindow = window; }
        return true;
    }

    private sealed class WindowSearchContext(uint processId)
    {
        internal uint ProcessId { get; } = processId;
        internal nint BestWindow { get; set; }
        internal long BestArea { get; set; }
    }
}
