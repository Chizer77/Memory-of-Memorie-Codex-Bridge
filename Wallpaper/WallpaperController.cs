using System.Runtime.InteropServices;
using MemoryOfMemorieCodexBridge.Windows;

namespace MemoryOfMemorieCodexBridge.Wallpaper;

internal sealed class WallpaperController
{
    private readonly nint target;
    private readonly bool compensateRemovedWindowFrame;
    private readonly int extraOverscanPixels;
    private readonly Action<string> info;
    private readonly Action<string> warning;
    private nint progman;
    private nint defView;
    private nint iconsWorker;
    private nint wallpaperWorker;
    private bool attached;
    private bool stateCaptured;
    private nint originalParent;
    private WindowRect originalRect;
    private int originalShowCommand;
    private nint originalStyle;
    private nint originalExtendedStyle;
    private WindowInsets originalFrameInsets;
    private bool workerIsChildOfProgman;

    internal WallpaperController(nint target, bool compensateRemovedWindowFrame, int extraOverscanPixels, Action<string> info, Action<string> warning)
    {
        this.target = target;
        this.compensateRemovedWindowFrame = compensateRemovedWindowFrame;
        this.extraOverscanPixels = extraOverscanPixels;
        this.info = info;
        this.warning = warning;
    }

    internal bool TryAttach(out string error)
    {
        error = string.Empty;
        if (!NativeMethods.IsWindow(target)) { error = "Game window is closed."; return false; }
        if (!TryInitializeDesktop(out error)) return false;

        if (!stateCaptured)
        {
            originalParent = NativeMethods.GetParent(target);
            NativeMethods.GetWindowRect(target, out originalRect);
            originalShowCommand = NativeMethods.IsZoomed(target) ? NativeMethods.SW_MAXIMIZE : NativeMethods.SW_RESTORE;
            originalStyle = NativeMethods.GetWindowLongPtr(target, NativeMethods.GWL_STYLE);
            originalExtendedStyle = NativeMethods.GetWindowLongPtr(target, NativeMethods.GWL_EXSTYLE);
            originalFrameInsets = CaptureOriginalFrameInsets();
            stateCaptured = true;
        }

        NativeMethods.ShowWindow(target, NativeMethods.SW_RESTORE);
        var childStyle = (nint)(((long)originalStyle | NativeMethods.WS_CHILD) & ~NativeMethods.WS_POPUP);
        NativeMethods.SetLastError(0);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_STYLE, childStyle);
        var styleError = Marshal.GetLastWin32Error();
        if (styleError != 0)
        {
            error = $"Cannot change game window style (Win32 error {styleError}).";
            NativeMethods.ShowWindow(target, originalShowCommand);
            return false;
        }

        var wallpaperExtendedStyle = (nint)(((long)originalExtendedStyle | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_EXSTYLE, wallpaperExtendedStyle);

        NativeMethods.SetLastError(0);
        var previousParent = NativeMethods.SetParent(target, wallpaperWorker);
        if (previousParent == 0 && Marshal.GetLastWin32Error() != 0)
        {
            error = $"Cannot attach game window to WorkerW (Win32 error {Marshal.GetLastWin32Error()}).";
            RestoreAfterFailedAttach();
            return false;
        }
        if (!ResizeToWorker())
        {
            error = "Cannot resize the game window to the desktop WorkerW.";
            RestoreAfterFailedAttach();
            return false;
        }

        TaskbarVisibility.Remove(target);

        if (workerIsChildOfProgman)
        {
            NativeMethods.ShowWindow(defView, NativeMethods.SW_HIDE);
            Thread.Sleep(0);
            NativeMethods.ShowWindow(defView, NativeMethods.SW_SHOW);
        }

        attached = true;
        info("Wallpaper mode attached.");
        return true;
    }

    internal void Detach(bool activate)
    {
        if (!attached) return;
        if (!NativeMethods.IsWindow(target)) { attached = false; return; }

        NativeMethods.SetParent(target, originalParent);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_STYLE, originalStyle);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_EXSTYLE, originalExtendedStyle);
        if (stateCaptured)
        {
            NativeMethods.SetWindowPos(target, 0, originalRect.Left, originalRect.Top, originalRect.Right - originalRect.Left, originalRect.Bottom - originalRect.Top,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
            NativeMethods.ShowWindow(target, originalShowCommand);
        }
        TaskbarVisibility.Restore(target);
        if (activate) NativeMethods.SetForegroundWindow(target);
        attached = false;
        info("Wallpaper mode detached.");
    }

    internal bool Maintain()
    {
        if (!attached || !NativeMethods.IsWindow(target) || !NativeMethods.IsWindow(progman) || !NativeMethods.IsWindow(defView)) return false;

        if (workerIsChildOfProgman)
        {
            var currentWorker = NativeMethods.FindWindowEx(progman, 0, "WorkerW", "");
            if (currentWorker == 0) return false;
            if (currentWorker != wallpaperWorker || NativeMethods.GetParent(target) != currentWorker)
            {
                wallpaperWorker = currentWorker;
                NativeMethods.SetParent(target, wallpaperWorker);
                ResizeToWorker();
                TaskbarVisibility.Remove(target);
                warning("Desktop layer changed; wallpaper mode was reattached.");
            }
            return true;
        }

        var splitWorker = NativeMethods.FindWindowEx(0, iconsWorker, "WorkerW", "");
        if (splitWorker == 0) return false;
        if (splitWorker != wallpaperWorker || NativeMethods.GetParent(target) != splitWorker)
        {
            wallpaperWorker = splitWorker;
            NativeMethods.SetParent(target, wallpaperWorker);
            ResizeToWorker();
            TaskbarVisibility.Remove(target);
            warning("Desktop layer changed; wallpaper mode was reattached.");
        }
        return true;
    }

    private bool TryInitializeDesktop(out string error)
    {
        error = string.Empty;
        progman = NativeMethods.FindWindowEx(0, 0, "Progman", "Program Manager");
        if (progman == 0) { error = "Explorer Progman window was not found."; return false; }

        SplitDesktopLayers();
        defView = NativeMethods.FindWindowEx(progman, 0, "SHELLDLL_DefView", "");
        if (defView != 0)
        {
            wallpaperWorker = NativeMethods.FindWindowEx(progman, 0, "WorkerW", "");
            if (wallpaperWorker != 0) { workerIsChildOfProgman = true; return true; }
            Thread.Sleep(100);
            if (TryFindSplitWorkers()) { workerIsChildOfProgman = false; return true; }
        }
        else if (TryFindSplitWorkers())
        {
            workerIsChildOfProgman = false;
            return true;
        }

        error = "A usable Explorer WorkerW wallpaper layer was not found.";
        return false;
    }

    private void SplitDesktopLayers()
    {
        NativeMethods.SendMessageTimeout(progman, NativeMethods.WM_USER + 300, 0, 0, NativeMethods.SMTO_NORMAL, 1000, out _);
        NativeMethods.SendMessageTimeout(progman, NativeMethods.WM_USER + 300, 0xD, 0, NativeMethods.SMTO_NORMAL, 1000, out _);
        NativeMethods.SendMessageTimeout(progman, NativeMethods.WM_USER + 300, 0xD, 1, NativeMethods.SMTO_NORMAL, 1000, out _);
    }

    private bool TryFindSplitWorkers()
    {
        iconsWorker = NativeMethods.FindWindowEx(0, 0, "WorkerW", "");
        while (iconsWorker != 0)
        {
            defView = NativeMethods.FindWindowEx(iconsWorker, 0, "SHELLDLL_DefView", "");
            if (defView != 0) break;
            iconsWorker = NativeMethods.FindWindowEx(0, iconsWorker, "WorkerW", "");
        }
        if (defView == 0) return false;
        wallpaperWorker = NativeMethods.FindWindowEx(0, iconsWorker, "WorkerW", "");
        return wallpaperWorker != 0;
    }

    private bool ResizeToWorker()
    {
        if (!NativeMethods.GetClientRect(wallpaperWorker, out var clientRect)) return false;
        var insets = compensateRemovedWindowFrame ? originalFrameInsets : WindowInsets.Empty;
        var x = -insets.Left - extraOverscanPixels;
        var y = -insets.Top - extraOverscanPixels;
        var width = clientRect.Right - clientRect.Left + insets.Left + insets.Right + extraOverscanPixels * 2;
        var height = clientRect.Bottom - clientRect.Top + insets.Top + insets.Bottom + extraOverscanPixels * 2;
        return NativeMethods.SetWindowPos(target, 0, x, y, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);
    }

    private WindowInsets CaptureOriginalFrameInsets()
    {
        if (!NativeMethods.GetClientRect(target, out var clientRect)) return WindowInsets.Empty;

        var clientOrigin = new WindowPoint();
        if (!NativeMethods.ClientToScreen(target, ref clientOrigin)) return WindowInsets.Empty;

        // Unity 可能保留旧客户区的渲染偏移；补回已移除的窗口框避免顶部留白。
        var left = Math.Max(0, clientOrigin.X - originalRect.Left);
        var top = Math.Max(0, clientOrigin.Y - originalRect.Top);
        var width = Math.Max(0, originalRect.Right - originalRect.Left);
        var height = Math.Max(0, originalRect.Bottom - originalRect.Top);
        var clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
        var clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
        return new WindowInsets(left, top, Math.Max(0, width - left - clientWidth), Math.Max(0, height - top - clientHeight));
    }

    private void RestoreAfterFailedAttach()
    {
        NativeMethods.SetParent(target, originalParent);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_STYLE, originalStyle);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_EXSTYLE, originalExtendedStyle);
        NativeMethods.SetWindowPos(target, 0, originalRect.Left, originalRect.Top, originalRect.Right - originalRect.Left, originalRect.Bottom - originalRect.Top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        NativeMethods.ShowWindow(target, originalShowCommand);
    }

    private readonly record struct WindowInsets(int Left, int Top, int Right, int Bottom)
    {
        internal static WindowInsets Empty { get; } = new(0, 0, 0, 0);
    }
}
