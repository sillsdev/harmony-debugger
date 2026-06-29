using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIL.Harmony;

namespace HarmonyDebuggerUi.Services.Import;

/// <summary>
/// Holds a queue of staged commits and applies them one-by-one to the local DataModel when requested.
/// </summary>
public sealed class PendingImportService
{
    private readonly DataModel _dataModel;
    private Queue<Commit> _queue = new();

    public PendingImportService(DataModel dataModel)
    {
        _dataModel = dataModel;
    }

    public int PendingCount => _queue.Count;
    public bool HasPending => _queue.Count > 0;

    public void SetBatch(ImportedCommitBatch batch)
    {
        _queue = new Queue<Commit>(batch.Commits);
    }

    /// <summary>
    /// Apply a single commit if available. Returns true if more remain.
    /// </summary>
    public async Task<bool> StepAsync()
    {
        if (_queue.Count == 0) return false;
        var next = _queue.Peek();
        // Apply using existing sync path primitive
    await ((ISyncable)_dataModel).AddRangeFromSync(new[] { next });
        _queue.Dequeue();
        return _queue.Count > 0;
    }

    /// <summary>
    /// Apply up to maxSteps commits (or all if null). Returns number applied.
    /// </summary>
    public async Task<int> StepManyAsync(int? maxSteps = null)
    {
        if (_queue.Count == 0) return 0;
        var applied = 0;
        while (HasPending && (maxSteps == null || applied < maxSteps.Value))
        {
            var moreRemain = await StepAsync();
            applied++;
            if (!moreRemain && !HasPending) break; // queue drained
        }
        return applied;
    }

    public void Clear() => _queue.Clear();
}
