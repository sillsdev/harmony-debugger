using Avalonia;
using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDebugger;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Build CRDT service collection with an in-memory SQLite placeholder so
            // CrdtConfig & type metadata are available immediately. The real file
            // path can be injected later by updating DbPathContext.DbPath.
            var services = CrdtLoader.LoadCrdt("Data Source=:memory:");
            BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Startup failed: {ex}");
            Environment.Exit(2);
        }
    }

    public static AppBuilder BuildAvaloniaApp(ServiceCollection? services = null)
        => AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
