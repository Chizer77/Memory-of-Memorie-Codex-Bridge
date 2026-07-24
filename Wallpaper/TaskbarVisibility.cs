using System.Runtime.InteropServices;

namespace MemoryOfMemorieCodexBridge.Wallpaper;

internal static class TaskbarVisibility
{
    internal static void Remove(nint window)
    {
        TryUpdate(window, taskbar => taskbar.DeleteTab(window));
    }

    internal static void Restore(nint window)
    {
        TryUpdate(window, taskbar => taskbar.AddTab(window));
    }

    private static void TryUpdate(nint window, Action<ITaskbarList> update)
    {
        try
        {
            var taskbar = (ITaskbarList)new TaskbarList();
            taskbar.HrInit();
            update(taskbar);
        }
        catch (COMException)
        {
            // Explorer 重启期间任务栏对象可能暂不可用，窗口样式仍能隐藏任务栏入口。
        }
    }
}

[ComImport]
[Guid("56FDF342-FD6D-11D0-958A-006097C9A090")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList
{
    void HrInit();
    void AddTab(nint window);
    void DeleteTab(nint window);
    void ActivateTab(nint window);
    void SetActiveAlt(nint window);
}

[ComImport]
[Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
internal class TaskbarList;
