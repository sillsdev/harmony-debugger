using System;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDebugger.UI.Services.Crdt;

public interface ICrdtScopeFactory
{
    bool IsInitialized { get; }
    void Initialize(string dbPath);
    IServiceScope CreateScope();
    IServiceScope? TryCreateScope();
    T? GetService<T>() where T : class;
}

/// <summary>
/// Builds and owns a standalone ServiceProvider containing CRDT data services. Allows
/// late initialization (after the UI starts) and re-initialization when the user picks
/// a different database file.
/// </summary>
public sealed class CrdtScopeFactory : ICrdtScopeFactory, IDisposable
{
    private readonly object _gate = new();
    private ServiceProvider? _provider;
    private string? _currentPath;

    public bool IsInitialized => _provider != null;

    public void Initialize(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentException("Path required", nameof(dbPath));
        lock (_gate)
        {
            // First initialization: build the provider once (Crdt types/config are stable)
            if (_provider == null)
            {
                var services = CrdtLoader.LoadCrdt(dbPath); // builds with mutable DbPathContext
                _provider = services.BuildServiceProvider();
                _currentPath = dbPath;
                return;
            }

            // Subsequent calls: only update path (DbPathContext is mutable and used by factory lambda)
            if (_currentPath == dbPath) return; // no change
            var ctx = _provider.GetService<DbPathContext>();
            if (ctx == null)
                throw new InvalidOperationException("DbPathContext missing from existing CRDT service provider");
            ctx.DbPath = dbPath; // updates target for future scopes/contexts
            _currentPath = dbPath;
        }
    }

    public IServiceScope CreateScope()
    {
        lock (_gate)
        {
            if (_provider == null) throw new InvalidOperationException("CRDT services not initialized");
            return _provider.CreateScope();
        }
    }

    public IServiceScope? TryCreateScope()
    {
        lock (_gate)
        {
            return _provider?.CreateScope();
        }
    }

    public T? GetService<T>() where T : class
    {
        lock (_gate)
        {
            if (_provider == null) return null;
            return _provider.GetService<T>();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _provider?.Dispose();
            _provider = null;
            _currentPath = null;
        }
    }
}
