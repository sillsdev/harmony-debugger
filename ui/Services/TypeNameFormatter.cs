using System;
using System.Linq;

namespace HarmonyDebugger.UI.Services;

internal static class TypeNameFormatter
{
    public static string PrettyTypeName(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var genericName = t.Name;
        var tickIndex = genericName.IndexOf('`');
        if (tickIndex > 0)
            genericName = genericName[..tickIndex];
        var argNames = t.GetGenericArguments().Select(a => a.Name);
        return $"{genericName}<{string.Join(',', argNames)}>`".TrimEnd('`');
    }
}
