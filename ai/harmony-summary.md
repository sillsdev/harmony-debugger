# SIL.Harmony CRDT Reference (Debugger-Oriented)

Provenance: Derived from the Harmony backend README & source (refresh after significant library/API changes).

## 1. Core Model Primitives
| Concept | Description |
|---------|-------------|
| Object | Domain entity (GUID) whose latest state is derivable from ordered changes. |
| Change | Intent (typed .NET class) – deterministic, pure except for entity mutation. |
| ChangeEntity | Persisted wrapper: (Id, CommitId, Index, SerializedChange JSON, metadata). |
| Commit | Ordered list of ChangeEntities + parent link + hybrid timestamp + author/client. |
| Snapshot | Optional materialized latest state (per object / whole project) for fast replay. |

Determinism: Replaying the same ordered Change sequence must yield identical state.

## 2. Commit Structure & Ordering
Essential fields (from model): Id, ParentCommitId (root null), HybridDateTime (logical+wall), ClientId / Author, ChangeEntities (ordered by Index).
Ordering guarantee: Stored sequentially (no persistent branching yet). Branch simulation = alternative replay path in debugger.

## 3. Change Types & Lifecycle
Base classes: `CreateChange<T>`, `EditChange<T>`, `Change<T>`, `DeleteChange<T>`.
Lifecycle:
1. Instantiate change (immutable payload recommended).
2. Serialize (JSON) -> ChangeEntity.
3. Apply during commit replay: `NewEntity()` (create) or `ApplyChange()` (edit/delete).
4. Update snapshots (if enabled).
No inverse operations; historical state = partial replay up to prior commit.

Determinism rules:
- No ambient time / randomness inside ApplyChange.
- Avoid external I/O or dependency ordering that could diverge.

## 4. Snapshots
Purpose: Reduce replay span for time-travel / object reconstruction.
Per-object snapshot: latest state after its last applied change.
Project snapshot (optional): baseline of all objects.
Reconstruct at commit N: if object snapshot <= N -> replay delta changes; else replay from creation.
Tradeoff: More frequent snapshots = faster debug stepping but higher write overhead.

## 5. DataModel High-Value API (grouped)
Mutation:
- `AddChange(clientId, change)` – single change commit.
- `AddChanges(clientId, IEnumerable<change>)` – batch commit (preferred for related edits).

Query latest:
- `GetLatest<T>(id)` – single entity state.
- `QueryLatest<T>()` – IQueryable for EF Core composition.

Historical:
- `GetAtCommit<T>(commit, id)`
- `GetAtTime<T>(timestamp, id)`

Snapshots:
- `GetLatestSnapshotByObjectId(id)`
- `GetProjectSnapshot(includeDeleted)`

Sync:
- `SyncWith(remote)` / `SyncMany(remotes)`

Sync internal helpers (via `ISyncable`):
- `GetSyncState()` – local vector / watermark state.
- `GetChanges(remoteState)` – compute missing commits for remote.
- `AddRangeFromSync(commits)` – ingest ordered commits.

## 6. Sync Protocol (Sequence)
1. Local: `state = GetSyncState()`
2. Remote: `missing = GetChanges(state)`
3. Local: `AddRangeFromSync(missing)` (preserve order) -> state advances.
4. (Optional bidirectional) repeat reversed roles.
ClientId must be stable per device/project; collisions cause lost increment detection.

## 7. Configuration Pattern
```csharp
services.AddCrdtData<AppDbContext>(cfg => {
  cfg.ObjectTypeListBuilder
    .Add<Word>()
    .Add<Definition>();
  cfg.ChangeTypeListBuilder
    .Add<SetWordTextChange>();
});
```
What it wires:
- Registers Harmony services (DataModel, internal stores, serializers).
- Freezes `CrdtConfig` after container build (don’t mutate later).
Pitfalls:
- Omitted change type => deserialization failure.
- Non-deterministic ApplyChange => divergent replicas.

## 8. Performance & Profiling Levers
- Batch related edits: `AddChanges` vs many `AddChange` calls.
- Introduce snapshots for hot objects (reduces time-travel latency).
- Filter early in `QueryLatest<T>()` to push predicates into SQL.
- Keep change payload minimal (stored forever & transmitted in sync).
- Potential debugger hook: wrap replay loop to time per-change application.

## 9. Common Edge / Failure Cases
| Symptom | Likely Cause | Check |
|---------|--------------|-------|
| Entity missing at historical commit | Replay position precedes its creation change | Confirm creation change appears before target commit |
| Divergent state across machines | Non-deterministic change logic | Inspect `ApplyChange` for time/random/external I/O |
| Change fails to deserialize | Change type not registered in `ChangeTypeListBuilder` | Ensure type added before config freeze; exception on load indicates failure |

## 10. Minimal Usage Snippet
```csharp
// Registration
services.AddCrdtData<AppDbContext>(cfg => {
  cfg.ObjectTypeListBuilder.Add<Word>().Add<Definition>();
  cfg.ChangeTypeListBuilder.Add<SetWordTextChange>();
});

// Add single change
await dataModel.AddChange(clientId, new SetWordTextChange(wordId, "Hello"));

// Batch changes (preferred)
await dataModel.AddChanges(clientId, new IChange[] {
  new SetWordTextChange(wordId, "Hello"),
  new SetWordTextChange(wordId, "Hello World")
});

// Query latest
var words = await dataModel.QueryLatest<Word>()
  .Where(w => w.Text.StartsWith("H"))
  .ToArrayAsync();

// Historical state
var old = await dataModel.GetAtCommit<Word>(commit, wordId);

// Sync
await dataModel.SyncWith(remoteModel);
```

## 11. Update Policy
Refresh after: new change base types, commit schema changes, snapshot mechanics updates, or sync protocol alterations.

---
This enriched reference is optimized for debugger development (replay, time-travel, diff, profiling).
