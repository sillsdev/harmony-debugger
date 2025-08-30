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
        if (crdtServices is null && !Design.IsDesignMode)
            throw new InvalidOperationException("Services must be provided in non-design mode");

        _services = crdtServices ?? new ServiceCollection();
    }

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
            });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ServiceProvider? provider = null;
        try
        {
            AddUiServices(_services);
            provider = _services.BuildServiceProvider();
            var crdtConfig = provider.GetRequiredService<IOptions<CrdtConfig>>().Value;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = provider.GetRequiredService<MainWindow>();
                desktop.Exit += (_, _) => provider.Dispose();
            }
        }
        catch (Exception ex)
        {
            provider?.Dispose();
            throw;
            ShowErrorWindow("Failed building services: " + ex.Message);
        }
        finally
        {
            base.OnFrameworkInitializationCompleted();
        }
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
