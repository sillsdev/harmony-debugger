using System;
using System.Threading;
using LcmCrdt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HarmonyDebugger;

/// <summary>
/// Holds the current SQLite connection string so that the EF Core factory created by AddCrdtCore
/// can always obtain the latest value. Swapping the value here and then creating a new scope /
/// resolving a fresh DbContextFactory will target the new database without rebuilding the container.
/// </summary>
public sealed class DbPathContext
{
    private string _dbPath;
    public DbPathContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public string DbPath
    {
        get => Volatile.Read(ref _dbPath);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Connection string cannot be empty", nameof(value));
            Volatile.Write(ref _dbPath, value);
        }
    }
}

public static class CrdtLoader
{

    /// <summary>
    /// Build the service collection for CRDT access. The connection string is stored in a mutable singleton
    /// so it can be updated at runtime. To switch databases, call <see cref="UpdateCrdtConnectionString"/>
    /// and then obtain a new DbContext scope/factory.
    /// </summary>
    public static ServiceCollection LoadCrdt(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentException("SQLite connection string is required.", nameof(dbPath));
        var services = new ServiceCollection();
        services.TryAddSingleton<IConfiguration>(_ => new ConfigurationRoot([]));
        services.AddSingleton(new DbPathContext(dbPath));

        // Resolve connection dynamically so future updates are picked up.
        LcmCrdtKernel.AddCrdtCore(services, sp => sp.GetRequiredService<DbPathContext>().DbPath);
        return services;
    }
}
