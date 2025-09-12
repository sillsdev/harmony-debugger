using System;
using CommunityToolkit.Mvvm.Input;
using SIL.Harmony;
using SIL.Harmony.Changes;
using SIL.Harmony.Core;
using System.Collections.Generic;

namespace HarmonyDebugger.UI.ViewModels;

public interface ICommitListRow { RowKind Kind { get; } }
public enum RowKind { Commit, Change }

public sealed class CommitRow : ViewModelBase, ICommitListRow
{
    private readonly Func<Commit, IReadOnlyList<ChangeEntity<IChange>>> _loader;
    private readonly Action<CommitRow> _expand;
    private readonly Action<CommitRow> _collapse;
    public Commit Commit { get; }
    // Prefetched change count (navigation collection may still be empty until expanded)
    public int ChangeCount { get; }
    public bool IsExpanded { get; internal set; }
    public RowKind Kind => RowKind.Commit;

    public CommitRow(Commit commit, int changeCount, Func<Commit, IReadOnlyList<ChangeEntity<IChange>>> loader, Action<CommitRow> expand, Action<CommitRow> collapse)
    {
        Commit = commit;
        ChangeCount = changeCount;
        _loader = loader;
        _expand = expand;
        _collapse = collapse;
    }

    public string HashShort => Commit.Hash.Length > 10 ? Commit.Hash.Substring(0, 10) : Commit.Hash;
    public string DateDisplay => $"{Commit.HybridDateTime.DateTime.ToString("yyyy-MM-dd HH:mm:ss")} ({Commit.HybridDateTime.Counter})";
    public string RelativeAge => GetRelative(Commit.HybridDateTime.DateTime);
    public string Glyph => IsExpanded ? "▼" : "▶";
    public string MetadataLine
    {
        get
        {
            var md = Commit.Metadata;
            var author = md?.AuthorName ?? "(unknown)";
            var version = md?.ClientVersion ?? "(unknown)";
            return $"Author: {author} | Client: {Commit.ClientId}{Environment.NewLine}Version: {version}";
        }
    }

    private RelayCommand? _toggleCommand;
    public System.Windows.Input.ICommand ToggleCommand => _toggleCommand ??= new RelayCommand(Toggle);

    public void Toggle()
    {
        if (IsExpanded) _collapse(this); else { _loader(Commit); _expand(this); }
    }

    internal void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(Glyph));
    }

    private static string GetRelative(DateTimeOffset dt)
    {
        var span = DateTimeOffset.UtcNow - dt;
        if (span.TotalSeconds < 60) return $"{(int)span.TotalSeconds}s ago";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("yyyy-MM-dd");
    }
}

public sealed class ChangeRow : ViewModelBase, ICommitListRow
{
    public ChangeEntity<IChange> Change { get; }
    public Guid ParentCommitId { get; }
    public RowKind Kind => RowKind.Change;
    public ChangeRow(Guid parentCommitId, ChangeEntity<IChange> change)
    {
        ParentCommitId = parentCommitId;
        Change = change;
    }
    public string TypeName => HarmonyDebugger.UI.Services.TypeNameFormatter.PrettyTypeName(Change.Change.GetType());
    public string EntityIdShort => Change.EntityId.ToString()[..8];
    public int Index => Change.Index;
}
