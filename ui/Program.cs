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
        var dbArg = $"{FindParentTestDataDir()}/sena-3.sqlite";
        // args.FirstOrDefault(a => a.StartsWith("--db="))?.Split('=')[1];
        if (string.IsNullOrWhiteSpace(dbArg))
        {
            Console.Error.WriteLine("Usage: Harmony.Debugger --db=Path/To/db.sqlite");
            Environment.Exit(1);
        }

        try
        {
            var services = CrdtLoader.LoadCrdt(dbArg);
            BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            throw;
            Console.Error.WriteLine($"Inspection failed: {ex.Message}");
            Environment.Exit(2);
            return;
        }
    }

    private static string? FindParentTestDataDir()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "test-data");
            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        return null;
    }

    public static AppBuilder BuildAvaloniaApp(ServiceCollection? services = null)
        => AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
