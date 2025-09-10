using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using HarmonyDebugger.UI.Services;

namespace HarmonyDebugger.UI.ViewModels;

public partial class TypesWindowViewModel : ViewModelBase
{
    public TypesWindowViewModel(IHarmonyConfigService configService)
    {
        ChangeTypeNames = configService.ChangeTypeNames;
        ObjectTypeNames = configService.ObjectTypeNames;
    }

    public IReadOnlyList<string> ChangeTypeNames { get; }
    public IReadOnlyList<string> ObjectTypeNames { get; }
}
