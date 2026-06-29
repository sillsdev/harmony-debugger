using System.Text.Json;
using SIL.Harmony;

namespace HarmonyDebuggerUi.Services.Import;

/// <summary>
/// Strategy for detecting and deserializing an incoming commit payload (e.g. JSON ChangesResult, raw commit list, future sqlite, etc.).
/// Implementations should be stateless and thread-safe.
/// </summary>
public interface ICommitImportFormat
{
    /// <summary>
    /// Lightweight probe to decide if this format can handle the given text (should not throw; return false if unsure).
    /// </summary>
    bool CanHandle(string inputSample);

    /// <summary>
    /// Parse the full input into an ordered set of commits ready for staged application.
    /// Should validate parent chain order internally; throw with a concise message if invalid.
    /// </summary>
    ImportedCommitBatch Deserialize(string input, JsonSerializerOptions jsonOptions);
}

/// <summary>
/// Result of a successful import parse: ordered commits plus optional metadata for UI display.
/// </summary>
public sealed record ImportedCommitBatch(Commit[] Commits, string? SourceDescription = null)
{
    public static ImportedCommitBatch Empty { get; } = new([], "empty");
}
