using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HarmonyDebuggerUi.Services.Import;

/// <summary>
/// Default implementation that tries registered formats in order.
/// </summary>
public sealed class IncomingCommitSource : IIncomingCommitSource
{
    private readonly IEnumerable<ICommitImportFormat> _formats;
    private readonly JsonSerializerOptions _jsonOptions;

    public IncomingCommitSource(IEnumerable<ICommitImportFormat> formats, JsonSerializerOptions jsonOptions)
    {
        _formats = formats;
        _jsonOptions = jsonOptions;
    }

    public Task<ImportedCommitBatch> LoadAsync(string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload)) return Task.FromResult(ImportedCommitBatch.Empty);

        var sample = rawPayload.Length > 500 ? rawPayload.Substring(0, 500) : rawPayload;
        var format = _formats.FirstOrDefault(f => f.CanHandle(sample));
        if (format == null)
            throw new InvalidOperationException("Unsupported import format (no handler recognized the payload)");

        var batch = format.Deserialize(rawPayload, _jsonOptions);
        return Task.FromResult(batch);
    }
}
