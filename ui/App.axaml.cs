using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using HarmonyDebugger.UI.ViewModels;
using HarmonyDebugger.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SIL.Harmony;

namespace HarmonyDebugger;

public partial class App : Application
{
    private readonly ServiceCollection _services;

    public App(ServiceCollection? crdtServices)
    {
        // Allow starting with an empty service collection; a DB can be loaded later.
        _services = crdtServices ?? new ServiceCollection();
    }

    public App() : this(new ServiceCollection()) { }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void AddUiServices(ServiceCollection services)
    {
        services
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<MainWindow>((sp) =>
            {
                return new MainWindow
                {
                    DataContext = sp.GetRequiredService<MainWindowViewModel>()
                };
            })
            .AddSingleton<HarmonyDebugger.UI.Services.IHarmonyConfigService>(sp =>
            {
                // Try construct real config service; fall back to null-object.
                try
                {
                    var cfg = sp.GetService<IOptions<CrdtConfig>>();
                    if (cfg is not null)
                        return new HarmonyDebugger.UI.Services.HarmonyConfigService(cfg);
                }
                catch { }
                return new HarmonyDebugger.UI.Services.NullHarmonyConfigService();
            })
            .AddTransient<TypesWindowViewModel>()
            .AddTransient<TypesWindow>(sp =>
            {
                return new TypesWindow
                {
                    DataContext = sp.GetRequiredService<TypesWindowViewModel>()
                };
            });
    }

    public override void OnFrameworkInitializationCompleted()
    {
    ServiceProvider? provider = null;
    AddUiServices(_services);
    provider = _services.BuildServiceProvider();
    // Attempt to resolve CrdtConfig only if it is registered.
    try { _ = provider.GetService<IOptions<CrdtConfig>>(); } catch { }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = provider.GetRequiredService<MainWindow>();
            desktop.Exit += (_, _) => provider.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void ShowErrorWindow(string message)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                // DataContext = new MainWindowViewModel { Greeting = message }
            };
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
