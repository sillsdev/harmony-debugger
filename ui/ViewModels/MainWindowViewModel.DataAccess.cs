using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIL.Harmony;
using SIL.Harmony.Changes;
using SIL.Harmony.Core;
using SIL.Harmony.Db;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace HarmonyDebugger.UI.ViewModels;

public partial class MainWindowViewModel
{
    private void TryLoadCommitsSafe()
    {
        try { LoadCommits(); } catch { }
    }

    private void LoadCommits()
    {
        using var scope = _rootProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ICrdtDbContextFactory>();
        using var ctx = factory.CreateDbContext();
        var cs = ctx.Database.GetConnectionString();
        if (!string.IsNullOrEmpty(cs))
            DatabaseName = GetDatabaseNameFromConnectionString(cs);

        var commitInfos = ctx.Commits
            .AsNoTracking()
            .Select(c => new { Commit = c, ChangeCount = c.ChangeEntities.Count })
            .ToList();

        _prefetchedCounts.Clear();
        foreach (var info in commitInfos)
            _prefetchedCounts[info.Commit.Id] = info.ChangeCount;

        commitInfos = commitInfos
            .OrderByDescending(x => x.Commit.HybridDateTime.DateTime)
            .ToList();

        _commits.Clear();
        foreach (var info in commitInfos)
            _commits.Add(info.Commit);

        OnPropertyChanged(nameof(CommitCount));
        RebuildRows();
        CombinedTypesStatus = _harmonyConfig.ConfigSummary;
    }

    private static string? FindParentTestDataDir()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "test-data");
            if (Directory.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return null;
    }

    [RelayCommand]
    private void OpenSena3()
    {
        try
        {
            var dir = FindParentTestDataDir();
            if (dir is null) return;
            var sena3Path = System.IO.Path.Combine(dir, "sena-3.sqlite");
            _dbPathContext.DbPath = sena3Path;
            TryLoadCommitsSafe();
        }
        catch (Exception ex) { LogDebug("OpenSena3 failed", ex); }
    }

    [RelayCommand]
    public async Task OpenDbFileAsync()
    {
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var owner = lifetime?.MainWindow;
            if (owner?.StorageProvider is null) return;
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
        catch (Exception ex) { LogDebug("OpenDbFileAsync failed", ex); }
    }

    public void SetDatabasePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            _dbPathContext.DbPath = path;
            TryLoadCommitsSafe();
        }
        catch (Exception ex) { LogDebug("SetDatabasePath failed", ex); }
    }

    private System.Windows.Input.ICommand? _openDbFileAsyncCommand;
    public System.Windows.Input.ICommand OpenDbFileAsyncCommand =>
        _openDbFileAsyncCommand ??= new AsyncRelayCommand(OpenDbFileAsync);

    private static string GetDatabaseNameFromConnectionString(string cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return "(no database)"; // unify wording
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
            { raw = value; break; }
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

    private IReadOnlyList<ChangeEntity<IChange>> EnsureChangesLoaded(Commit commit)
    {
        if (commit.ChangeEntities.Count > 0)
            return commit.ChangeEntities;
        using var scope = _rootProvider.CreateScope();
        var factory = scope.ServiceProvider.GetService<ICrdtDbContextFactory>();
        if (factory is null) return commit.ChangeEntities;
        using var ctx = factory.CreateDbContext();
        var changes = ctx.Set<ChangeEntity<IChange>>()
            .Where(ce => ce.CommitId == commit.Id)
            .OrderBy(ce => ce.Index)
            .AsNoTracking()
            .ToList();
        commit.ChangeEntities.AddRange(changes);
        return commit.ChangeEntities;
    }

    private static System.Reflection.MethodInfo? _genericGetAtCommitMethod;
    private readonly Dictionary<Type, System.Reflection.MethodInfo> _closedGetAtCommitCache = new();

    private async Task<(object? entity, string? error)> TryGetEntityStateAtChangeAsync(ChangeRow changeRow)
    {
        // Retrieve the entity state AS OF (after) the commit containing this change.
        // We intentionally do not swallow exceptions; we return the message so the UI can display it.
        await using var scope = _rootProvider.CreateAsyncScope();
        var dataModel = scope.ServiceProvider.GetService<DataModel>();
        if (dataModel is null)
            return (null, "DataModel service not available");

        var commitId = changeRow.ParentCommitId;
        var entityId = changeRow.Change.Change.EntityId;
        var entityType = changeRow.Change.Change.EntityType;

        // Cache open generic method
        _genericGetAtCommitMethod ??= typeof(DataModel).GetMethod("GetAtCommit", new[] { typeof(Guid), typeof(Guid) });
        if (_genericGetAtCommitMethod == null)
            return (null, "GetAtCommit method not found on DataModel");

        try
        {
            if (!_closedGetAtCommitCache.TryGetValue(entityType, out var generic))
            {
                generic = _genericGetAtCommitMethod.MakeGenericMethod(entityType);
                _closedGetAtCommitCache[entityType] = generic;
            }
            var task = (Task)generic.Invoke(dataModel, new object[] { commitId, entityId })!;
            await task.ConfigureAwait(false);
            var resultProp = task.GetType().GetProperty("Result");
            var entity = resultProp?.GetValue(task);
            return (entity, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
