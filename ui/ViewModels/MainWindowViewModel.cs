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
using HarmonyDebugger.UI.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace HarmonyDebugger.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(IServiceProvider serviceProvider, DbPathContext dbPathContext, HarmonyDebugger.UI.Services.IHarmonyConfigService harmonyConfig)
    {
        _rootProvider = serviceProvider;
        _dbPathContext = dbPathContext;
        _harmonyConfig = harmonyConfig;
        Commits = new ReadOnlyObservableCollection<Commit>(_commits);
        // We always have CRDT config (types) at startup, but we defer any DB access
        // until the user explicitly selects a database.
        CombinedTypesStatus = _harmonyConfig.ConfigSummary;

#if DEBUG
        OpenSena3();
#endif
    }

    private readonly IServiceProvider _rootProvider;
    private readonly DbPathContext _dbPathContext;
    private readonly HarmonyDebugger.UI.Services.IHarmonyConfigService _harmonyConfig;


    // PrettyTypeName logic moved to Services.TypeNameFormatter.
    public int CommitCount => _commits.Count;

    private string _databaseName = "(no database)";
    public string DatabaseName
    {
        get => _databaseName;
        private set => SetProperty(ref _databaseName, value);
    }

    private readonly ObservableCollection<Commit> _commits = new();
    public ReadOnlyObservableCollection<Commit> Commits { get; }

    public HierarchicalTreeDataGridSource<ICommitTreeItem>? CommitTree { get; private set; }

    private string _combinedTypesStatus = string.Empty;
    public string CombinedTypesStatus
    {
        get => _combinedTypesStatus;
        private set => SetProperty(ref _combinedTypesStatus, value);
    }

    private void TryLoadCommitsSafe()
    {
        try
        {
            LoadCommits();
        }
        catch
        {
            // Suppress errors if DB not yet configured.
        }
    }

    private void LoadCommits()
    {
        using var scope = _rootProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ICrdtDbContextFactory>();
        using var ctx = factory.CreateDbContext();
        var cs = ctx.Database.GetConnectionString();
        if (!string.IsNullOrEmpty(cs))
        {
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

        // Update status bar summary
        CombinedTypesStatus = _harmonyConfig.ConfigSummary;
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

    [RelayCommand]
    private void OpenSena3()
    {
        var sena3Path = $"{FindParentTestDataDir()}/sena-3.sqlite";
        _dbPathContext.DbPath = sena3Path;
        TryLoadCommitsSafe();
    }

    /// <summary>
    /// Opens a file dialog so the user can pick a SQLite database and loads it.
    /// </summary>
    [RelayCommand]
    public async System.Threading.Tasks.Task OpenDbFileAsync()
    {
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var owner = lifetime?.MainWindow;
            if (owner is null) return;
            if (owner.StorageProvider is null) return;
            var results = await owner.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<Avalonia.Platform.Storage.FilePickerFileType>
                {
                    new("SQLite Database") { Patterns = new List<string>{ "*.sqlite", "*.db" } },
                    new("All Files") { Patterns = new List<string>{ "*.*" } }
                }
            });
            var file = results?.FirstOrDefault();
            var path = file?.Path?.LocalPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            SetDatabasePath(path);
        }
        catch { }
    }

    /// <summary>
    /// Sets the database path and reloads commits. Can be called from drag-and-drop or picker.
    /// </summary>
    public void SetDatabasePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            _dbPathContext.DbPath = path;
            TryLoadCommitsSafe();
        }
        catch { }
    }

    // Manual command wrapper for XAML binding (OpenDbFileAsyncCommand)
    private System.Windows.Input.ICommand? _openDbFileAsyncCommand;
    public System.Windows.Input.ICommand OpenDbFileAsyncCommand =>
        _openDbFileAsyncCommand ??= new AsyncRelayCommand(OpenDbFileAsync);

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
    if (string.IsNullOrWhiteSpace(raw)) return "(no database)";
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
    public string DisplayText => HarmonyDebugger.UI.Services.TypeNameFormatter.PrettyTypeName(Entity.Change.GetType());
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
    var factory = scope.ServiceProvider.GetService<ICrdtDbContextFactory>();
    if (factory is null) return commit.ChangeEntities; // no DB yet
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

    [RelayCommand]
    private void OpenTypesWindow()
    {
        try
        {
            if (_typesWindow != null)
            {
                if (_typesWindow.IsVisible)
                {
                    _typesWindow.Activate();
                    return;
                }
                else
                {
                    _typesWindow = null;
                }
            }
            var window = _rootProvider.GetService(typeof(TypesWindow)) as TypesWindow;
            if (window is null) return;
            _typesWindow = window;
            _typesWindow.Closed += (_, _) => _typesWindow = null;
            _typesWindow.Show();
            _typesWindow.Activate();
        }
        catch { _typesWindow = null; }
    }

    private TypesWindow? _typesWindow;
}
