using SIL.Harmony;
using System.Linq;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using HarmonyDebugger.UI.Views;
using System.Text.Json;

namespace HarmonyDebugger.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        DbPathContext dbPathContext,
        HarmonyDebugger.UI.Services.IHarmonyConfigService harmonyConfig,
        JsonSerializerOptions jsonSerializerOptions)
    {
        _rootProvider = serviceProvider;
        _dbPathContext = dbPathContext;
        _harmonyConfig = harmonyConfig;
        // keep original (shared) options for any future advanced scenarios, but don't mutate them
        _jsonOptions = jsonSerializerOptions;
        // create a pretty-print clone so we don't affect global persistence settings
        _jsonPrettyOptions = new JsonSerializerOptions(_jsonOptions)
        {
            WriteIndented = true
        };
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

    private string _combinedTypesStatus = string.Empty;
    public string CombinedTypesStatus
    {
        get => _combinedTypesStatus;
        private set => SetProperty(ref _combinedTypesStatus, value);
    }

    public ObservableCollection<ICommitListRow> Rows { get; } = new();
    private readonly Dictionary<Guid,int> _prefetchedCounts = new();

    private void RebuildRows()
    {
        Rows.Clear();
    IEnumerable<Commit> ordered = _commits.OrderByDescending(c => c.HybridDateTime.DateTime);
        foreach (var c in ordered)
        {
            var count = _prefetchedCounts.TryGetValue(c.Id, out var cc) ? cc : c.ChangeEntities.Count;
            Rows.Add(new CommitRow(c, count, EnsureChangesLoaded, ExpandCommit, CollapseCommit));
        }
    }

    private void ExpandCommit(CommitRow row)
    {
        if (row.IsExpanded) return;
        var changes = EnsureChangesLoaded(row.Commit);
        var insertIndex = Rows.IndexOf(row) + 1;
        for (int i = 0; i < changes.Count; i++)
        {
            Rows.Insert(insertIndex + i, new ChangeRow(row.Commit.Id, changes[i]));
        }
        row.IsExpanded = true;
        row.NotifyStateChanged();
    }

    private void CollapseCommit(CommitRow row)
    {
        if (!row.IsExpanded) return;
        var start = Rows.IndexOf(row) + 1;
        while (start < Rows.Count && Rows[start] is ChangeRow cr && cr.ParentCommitId == row.Commit.Id)
        {
            Rows.RemoveAt(start);
        }
        row.IsExpanded = false;
        row.NotifyStateChanged();
    }

    // Database & dialog methods are moved to MainWindowViewModel.DataAccess.cs

    private ICommitListRow? _selectedRow;
    public ICommitListRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                _ = UpdateJsonDetailsAsync();
            }
        }
    }
    public CommitRow? SelectedCommit => SelectedRow as CommitRow;
    public ChangeRow? SelectedChange => SelectedRow as ChangeRow;

    private string _selectedCommitJson = string.Empty;
    public string SelectedCommitJson
    {
        get => _selectedCommitJson;
        private set => SetProperty(ref _selectedCommitJson, value);
    }

    private string _selectedEntityJson = string.Empty;
    public string SelectedEntityJson
    {
        get => _selectedEntityJson;
        private set => SetProperty(ref _selectedEntityJson, value);
    }

    // Fire-and-forget wrapper uses async Task; caller discards safely.
    private async System.Threading.Tasks.Task UpdateJsonDetailsAsync()
    {
        try
        {
            if (SelectedCommit is { } commitRow)
            {
                SelectedCommitJson = JsonSerializer.Serialize(BuildCommitSummary(commitRow), _jsonPrettyOptions);
                SelectedEntityJson = string.Empty;
                return;
            }
            if (SelectedChange is { } changeRow)
            {
                SelectedCommitJson = JsonSerializer.Serialize(changeRow.Change, _jsonPrettyOptions);
                SelectedEntityJson = "(loading snapshot ...)";
                var (entity, error) = await TryGetEntityStateAtChangeAsync(changeRow);
                if (error != null)
                {
                    SelectedEntityJson = $"Snapshot error: {error}";
                    return;
                }
                if (entity == null)
                {
                    SelectedEntityJson = "(no snapshot available)";
                    return;
                }
                try
                {
                    SelectedEntityJson = JsonSerializer.Serialize(entity, _jsonPrettyOptions);
                }
                catch (Exception sx)
                {
                    SelectedEntityJson = $"Snapshot serialize error: {sx.Message}";
                }
                return;
            }
            // nothing selected
            SelectedCommitJson = string.Empty;
            SelectedEntityJson = string.Empty;
        }
        catch (Exception ex)
        {
            SelectedCommitJson = $"Error: {ex.Message}";
            SelectedEntityJson = string.Empty;
            LogDebug("UpdateJsonDetailsAsync failed", ex);
        }
    }
    private object BuildCommitSummary(CommitRow row)
    {
        // Use prefetched count when possible (cheaper than materializing changes if not expanded)
        var count = row.ChangeCount;
        var commit = row.Commit;
        return new
        {
            commit.Id,
            commit.HybridDateTime,
            commit.ClientId,
            ChangeEntities = $"[{count} changes]"
        };
    }
    private readonly JsonSerializerOptions _jsonOptions; // shared (unmodified) instance from DI (contains polymorphic config)
    private readonly JsonSerializerOptions _jsonPrettyOptions; // cloned & indented for UI display

    [System.Diagnostics.Conditional("DEBUG")]
    private void LogDebug(string message, Exception? ex = null)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindowVM] {message} {ex?.Message}");
        }
        catch { /* swallow logging failures */ }
    }
}

partial class MainWindowViewModel
{
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
