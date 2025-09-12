# Harmony Debugger Roadmap

Ordered list of confirmed features we will build using current Harmony APIs. Focus: what, not how.

## 1. Baseline Data Access (Done)
- Load Harmony config at startup without requiring a DB.
- Open a SQLite Harmony DB (file picker / drag & drop).
- Display commits with lazy change expansion.
- List registered change types.

## 2. Local Commit Stepping
- Iterate existing commits already stored in the opened database (no new commits added).
- Apply commits one at a time or in fixed batch sizes (1, 10, all remaining).
- Stop on first failure and show the failing commit and change index.
- Provide a reset to re-run from the start.

## 3. Entity Change Display & Diffs
- For each stepped commit, list affected entities.
- Show before vs after state for each affected entity.
- Highlight added, removed, and modified fields.

## 4. Incremental Sync Ingest Stepping
- Accept a batch of newly received commits (simulated sync source).
- Queue and step through them with same controls (1, 10, all) before merging into main view.
- Attribute failures to incoming commit/change without affecting existing history display until applied.
- After success, append them to the local commit list.

## 5. Snapshot Regeneration
- Rebuild all snapshots from scratch.
- Compare regenerated snapshots to prior ones.
- Report objects whose state differs.

## 6. Snapshot Drift Report
- Persist last regeneration comparison result for inspection until next run.
- Surface count of mismatched objects and allow inspection of each difference.

## 7. Error Visibility
- Central panel or area listing replay and deserialization errors (commit id, change index, message).

## Out of Scope (Not on this roadmap)
- Branch simulation.
- Profiling/timing UI.
- DAG/branch visualization.
- Patch editing / speculative change application.
