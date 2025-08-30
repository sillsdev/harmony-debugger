using Microsoft.Extensions.Options;
using SIL.Harmony;
using SIL.Harmony.Db;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls.Models.TreeDataGrid;
using SIL.Harmony.Core;
using SIL.Harmony.Changes;
using Avalonia.Controls;

namespace HarmonyDebugger.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(IOptions<CrdtConfig> crdtConfig, IServiceProvider serviceProvider, DbPathContext dbPathContext)
    {
        _crdtConfig = crdtConfig;
        _rootProvider = serviceProvider;
        _dbPathContext = dbPathContext;
        ChangeTypeNames = _crdtConfig.Value.ChangeTypes.Select(t => PrettyTypeName(t)).OrderBy(n => n).ToList();
        ObjectTypeNames = _crdtConfig.Value.ObjectTypes.Select(t => PrettyTypeName(t)).OrderBy(n => n).ToList();
        Commits = new ReadOnlyObservableCollection<Commit>(_commits);
        // initial load
        LoadCommits();
    }

    private readonly IOptions<CrdtConfig> _crdtConfig;
    private readonly IServiceProvider _rootProvider;
    private readonly DbPathContext _dbPathContext;
    private string? _lastConnectionString;


    public static string PrettyTypeName(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var genericName = t.Name;
        var tickIndex = genericName.IndexOf('`');
        if (tickIndex > 0)
            genericName = genericName[..tickIndex];
        var argNames = t.GetGenericArguments().Select(a => a.Name);
        return $"{genericName}<{string.Join(',', argNames)}>";
    }

    public IReadOnlyList<string> ChangeTypeNames { get; }

    public IReadOnlyList<string> ObjectTypeNames { get; }

    public int ChangeTypeCount => ChangeTypeNames.Count;
    public int ObjectTypeCount => ObjectTypeNames.Count;
    public int CommitCount => _commits.Count;

    private string _databaseName = "(db)";
    public string DatabaseName
    {
        get => _databaseName;
        private set => SetProperty(ref _databaseName, value);
    }

    private readonly ObservableCollection<Commit> _commits = new();
    public ReadOnlyObservableCollection<Commit> Commits { get; }

    public HierarchicalTreeDataGridSource<ICommitTreeItem>? CommitTree { get; private set; }

    private void LoadCommits()
    {
        // Create a fresh scope so the scoped AddDbContextFactory is rebuilt with current DbPath
        using var scope = _rootProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ICrdtDbContextFactory>();
        using var ctx = factory.CreateDbContext();
        var cs = ctx.Database.GetConnectionString();
        if (!string.IsNullOrEmpty(cs))
        {
            _lastConnectionString = cs;
            DatabaseName = GetDatabaseNameFromConnectionString(cs);
        }
        // Eager load ChangeEntities so the UI binding {Binding ChangeEntities.Count} shows the real value.
        // Query commits with change counts (no eager loading of changes) for perf.
        var commitInfos = ctx.Commits
            .AsNoTracking()
            .Select(c => new { Commit = c, ChangeCount = c.ChangeEntities.Count })
            .OrderByDescending(x => x.Commit.HybridDateTime.DateTime)
            .ToList();

        _commits.Clear();
        var roots = new List<ICommitTreeItem>(commitInfos.Count);
        foreach (var info in commitInfos)
        {
            _commits.Add(info.Commit);
            roots.Add(new CommitTreeItem(info.Commit, info.ChangeCount, EnsureChangesLoaded));
        }
        OnPropertyChanged(nameof(CommitCount));

        CommitTree = CommitTreeBuilder.Build(roots);
        OnPropertyChanged(nameof(CommitTree));
    }

    [RelayCommand]
    private void ReloadDb()
    {
        // Example hard-coded alternate path; in a real app this would be user-selected
        _dbPathContext.DbPath = "D:/code/harmony-debugger/test-data/sena-3.sqlite";
        LoadCommits();
    }

    private static string GetDatabaseNameFromConnectionString(string cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return "(no connection)";
        string? raw = null;
        foreach (var segment in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kvp = segment.Split('=', 2);
            if (kvp.Length != 2) continue;
            var key = kvp[0].Trim();
            var value = kvp[1].Trim();
            if (key.Equals("Data Source", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("DataSource", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Filename", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                raw = value; break;
            }
        }
        if (string.IsNullOrWhiteSpace(raw)) return "(db)";
        try
        {
            var fileName = Path.GetFileName(raw);
            if (!string.IsNullOrEmpty(fileName))
            {
                var noExt = Path.GetFileNameWithoutExtension(fileName);
                return string.IsNullOrEmpty(noExt) ? fileName : noExt;
            }
        }
        catch { }
        return raw;
    }
}

public interface ICommitTreeItem
{
    bool HasChildren { get; }
    IReadOnlyList<ICommitTreeItem>? Children { get; }
    string Hash { get; }
    string DisplayText { get; }
    string DateTimeDisplay { get; }
}

public sealed class CommitTreeItem : ICommitTreeItem
{
    private readonly Func<Commit, IReadOnlyList<ChangeEntity<IChange>>> _changeLoader;
    private List<ICommitTreeItem>? _children;

    public CommitTreeItem(Commit commit, int changeCount, Func<Commit, IReadOnlyList<ChangeEntity<IChange>>> loader)
    {
        Commit = commit;
        ChangeCount = changeCount;
        _changeLoader = loader;
    }

    public Commit Commit { get; }
    public int ChangeCount { get; }

    public bool HasChildren => ChangeCount > 0;
    public IReadOnlyList<ICommitTreeItem>? Children
    {
        get
        {
            if (!HasChildren) return null;
            if (_children != null) return _children;
            var changes = _changeLoader(Commit);
            _children = changes.Select(c => (ICommitTreeItem)new ChangeEntityTreeItem(c)).ToList();
            return _children;
        }
    }

    public string Hash => Commit.Hash;
    public string DisplayText => ChangeCount + " changes";
    public string DateTimeDisplay => Commit.HybridDateTime.DateTime.ToString("yyyy-MM-dd HH:mm:ss");
}

public sealed class ChangeEntityTreeItem : ICommitTreeItem
{
    public ChangeEntityTreeItem(ChangeEntity<IChange> entity)
    {
        Entity = entity;
    }
    public ChangeEntity<IChange> Entity { get; }
    public bool HasChildren => false;
    public IReadOnlyList<ICommitTreeItem>? Children => null;
    public string Hash => "";
    public string DisplayText => MainWindowViewModel.PrettyTypeName(Entity.Change.GetType());
    public string DateTimeDisplay => string.Empty;
}

internal static class CommitTreeBuilder
{
    public static HierarchicalTreeDataGridSource<ICommitTreeItem> Build(IReadOnlyList<ICommitTreeItem> roots)
    {
        var source = new HierarchicalTreeDataGridSource<ICommitTreeItem>(roots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<ICommitTreeItem>(
                    new TextColumn<ICommitTreeItem, string>("Info", n => n.DisplayText),
                    n => n.Children ?? Array.Empty<ICommitTreeItem>(),
                    n => n.HasChildren
                ),
                new TextColumn<ICommitTreeItem, string>("Date", n => n.DateTimeDisplay),
                new TextColumn<ICommitTreeItem, string>("Hash", n => n.Hash)
            }
        };
        return source;
    }
}

partial class MainWindowViewModel
{
    // Loads changes into the existing commit instance (so ChangeEntities.Count reflects reality).
    private IReadOnlyList<ChangeEntity<IChange>> EnsureChangesLoaded(Commit commit)
    {
        if (commit.ChangeEntities.Count > 0)
            return commit.ChangeEntities; // already loaded

        using var scope = _rootProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ICrdtDbContextFactory>();
        using var ctx = factory.CreateDbContext();
        var changes = ctx.Set<ChangeEntity<IChange>>()
            .Where(ce => ce.CommitId == commit.Id)
            .OrderBy(ce => ce.Index)
            .AsNoTracking()
            .ToList();
        // mutate the existing list so any references to commit.ChangeEntities see the data
        commit.ChangeEntities.AddRange(changes);
        return commit.ChangeEntities;
    }
}
