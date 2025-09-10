using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using SIL.Harmony;

namespace HarmonyDebugger.UI.Services;

public interface IHarmonyConfigService
{
    IReadOnlyList<string> ChangeTypeNames { get; }
    IReadOnlyList<string> ObjectTypeNames { get; }
    string TypesInfo { get; }
    string AssemblyInfo { get; }
    string ConfigSummary { get; }
    string PrettyTypeName(Type t);
}

public sealed class HarmonyConfigService : IHarmonyConfigService
{
    public HarmonyConfigService(IOptions<CrdtConfig> crdtConfig)
    {
        var cfg = crdtConfig.Value;
        ChangeTypeNames = cfg.ChangeTypes.Select(TypeNameFormatter.PrettyTypeName).OrderBy(n => n).ToList();
        ObjectTypeNames = cfg.ObjectTypes.Select(TypeNameFormatter.PrettyTypeName).OrderBy(n => n).ToList();
        TypesInfo = $"{ChangeTypeNames.Count} change types, {ObjectTypeNames.Count} object types";
        try
        {
            var assemblies = cfg.ChangeTypes.Select(t => t.Assembly.GetName().Name)
                .Concat(cfg.ObjectTypes.Select(t => t.Assembly.GetName().Name))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            AssemblyInfo = string.Join(", ", assemblies);
        }
        catch { AssemblyInfo = string.Empty; }
    }

    public IReadOnlyList<string> ChangeTypeNames { get; }
    public IReadOnlyList<string> ObjectTypeNames { get; }
    public string TypesInfo { get; }
    public string AssemblyInfo { get; }
    public string ConfigSummary => string.IsNullOrEmpty(AssemblyInfo) ? TypesInfo : $"{TypesInfo} | {AssemblyInfo}";

    public string PrettyTypeName(Type t)
    {
        return TypeNameFormatter.PrettyTypeName(t);
    }
}

/// <summary>
/// Fallback implementation used when no CrdtConfig is yet available.
/// </summary>
public sealed class NullHarmonyConfigService : IHarmonyConfigService
{
    public IReadOnlyList<string> ChangeTypeNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> ObjectTypeNames { get; } = Array.Empty<string>();
    public string TypesInfo => "0 change types, 0 object types";
    public string AssemblyInfo => string.Empty;
    public string ConfigSummary => TypesInfo;
    public string PrettyTypeName(Type t) => t.Name;
}