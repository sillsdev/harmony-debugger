using System.Threading.Tasks;

namespace HarmonyDebuggerUi.Services.Import;

/// <summary>
/// Abstraction over a user-provided payload (text, file path, etc.) that can be resolved into a commit batch via registered formats.
/// </summary>
public interface IIncomingCommitSource
{
    /// <summary>
    /// Attempt loading and parsing the provided payload using known formats.
    /// Throws on structural/validation errors, returns Empty on no commits.
    /// </summary>
    Task<ImportedCommitBatch> LoadAsync(string rawPayload);
}
