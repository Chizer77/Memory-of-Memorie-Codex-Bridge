using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace MemoryOfMemorieCodexBridge.Game;

internal static class Il2CppReflection
{
    internal static Type FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, false))
            .FirstOrDefault(type => type is not null);
    }

    internal static object FindUnityObject(Type targetType)
    {
        var classPointer = Il2CppClassPointerStore.GetNativeClassPointer(targetType);
        if (classPointer == IntPtr.Zero) return null;

        var il2CppType = Il2CppType.TypeFromPointer(classPointer, targetType.FullName ?? targetType.Name);
        var found = UnityEngine.Object.FindFirstObjectByType(il2CppType, FindObjectsInactive.Include)
            ?? UnityEngine.Object.FindObjectOfType(il2CppType, true);
        if (found is not null) return found;

        var allObjects = Resources.FindObjectsOfTypeAll(il2CppType);
        if (allObjects is null) return null;

        foreach (var candidate in allObjects)
        {
            if (candidate is not null) return candidate;
        }

        return null;
    }

    internal static object WrapAsTargetType(object target, Type targetType)
    {
        if (targetType.IsInstanceOfType(target)) return target;
        if (target is not Il2CppObjectBase il2CppObject) return null;

        return Activator.CreateInstance(targetType, il2CppObject.Pointer);
    }

    internal static object ReadInstanceProperty(object target, Type targetType, string propertyName)
    {
        if (target is null || targetType is null) return null;
        return targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
    }

    internal static string ReadInstanceString(object target, Type targetType, string propertyName)
    {
        return ReadInstanceProperty(target, targetType, propertyName)?.ToString() ?? string.Empty;
    }

    internal static void SetInstanceInt(object target, Type targetType, string propertyName, int value)
    {
        var property = targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property?.CanWrite == true) property.SetValue(target, value);
    }

    internal static void SetText(object textControl, string value)
    {
        var property = textControl?.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property?.CanWrite == true) property.SetValue(textControl, value);
    }

    internal static void InvokeInstanceVoid(object target, Type targetType, string methodName, params object[] arguments)
    {
        var parameterTypes = arguments.Select(argument => argument.GetType()).ToArray();
        var method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);
        method?.Invoke(target, arguments);
    }

    internal static long InvokeStaticLong(Type type, string methodName)
    {
        if (type is null) return 0;

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
        var value = method?.Invoke(null, null);
        return value is null ? 0 : Convert.ToInt64(value);
    }
}
