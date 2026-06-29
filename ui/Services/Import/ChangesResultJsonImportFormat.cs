using System;
using System.Linq;
using System.Text.Json;
using SIL.Harmony;
using SIL.Harmony.Core;

namespace HarmonyDebuggerUi.Services.Import;

/// <summary>
/// Parses a JSON payload shaped like ChangesResult&lt;Commit&gt; (properties missingFromClient, serverSyncState).
/// Treats result.MissingFromClient as the ordered queue to apply.
/// </summary>
public sealed class ChangesResultJsonImportFormat : ICommitImportFormat
{
    public bool CanHandle(string inputSample)
    {
        if (string.IsNullOrWhiteSpace(inputSample)) return false;
        // Cheap heuristic: must contain both property names
        return inputSample.Contains("missingFromClient", StringComparison.OrdinalIgnoreCase)
               && inputSample.Contains("serverSyncState", StringComparison.OrdinalIgnoreCase);
    }

    public ImportedCommitBatch Deserialize(string input, JsonSerializerOptions jsonOptions)
    {
        var result = JsonSerializer.Deserialize<ChangesResult<Commit>>(input, jsonOptions);
        if (result == null) throw new InvalidOperationException("Invalid ChangesResult JSON");
        var commits = result.MissingFromClient ?? Array.Empty<Commit>();
        if (commits.Length == 0) return ImportedCommitBatch.Empty;

        // Parent chain validation (weak: ensures each next parent equals previous hash or NullParent if first)
        string? previousHash = null;
        for (int i = 0; i < commits.Length; i++)
        {
            var c = commits[i];
            if (i == 0)
            {
                // first commit parent hash allowed to be NullParentHash or any (we can't verify against local here)
                previousHash = c.Hash; // we need Hash, but Commit.Hash is private set; assume deserialization filled it
                continue;
            }
            if (c.ParentHash != previousHash)
            {
                throw new InvalidOperationException($"Parent chain mismatch at index {i}: expected parent {previousHash} but found {c.ParentHash}");
            }
            previousHash = c.Hash;
        }

        // Ensure stable order already (assumed). Optionally sort by HybridDateTime if desired.
        return new ImportedCommitBatch(commits.ToArray(), $"ChangesResult JSON ({commits.Length} commits)");
    }
}
