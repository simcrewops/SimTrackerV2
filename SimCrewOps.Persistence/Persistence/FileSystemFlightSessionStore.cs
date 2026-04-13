using System.Text.Json;
using System.Text.Json.Serialization;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;

namespace SimCrewOps.Persistence.Persistence;

public sealed class FileSystemFlightSessionStore : IFlightSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    private readonly FileSystemFlightSessionStoreOptions _options;

    public FileSystemFlightSessionStore(FileSystemFlightSessionStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.RootDirectory))
        {
            throw new ArgumentException("Root directory is required.", nameof(options));
        }

        _options = options;
    }

    public async Task SaveCurrentSessionAsync(FlightSessionRuntimeState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var snapshot = new PersistedCurrentSession
        {
            SavedUtc = DateTimeOffset.UtcNow,
            State = state,
        };

        await WriteJsonAsync(GetCurrentSessionPath(), snapshot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistedCurrentSession?> LoadCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        var path = GetCurrentSessionPath();
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PersistedCurrentSession>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public Task ClearCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetCurrentSessionPath();
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task<PendingCompletedSession> QueueCompletedSessionAsync(
        FlightSessionRuntimeState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var record = new PendingCompletedSession
        {
            SessionId = Guid.NewGuid().ToString("n"),
            SavedUtc = DateTimeOffset.UtcNow,
            State = state,
        };

        await WriteJsonAsync(GetCompletedSessionPath(record.SessionId), record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task<IReadOnlyList<PendingCompletedSession>> ListCompletedSessionsAsync(CancellationToken cancellationToken = default)
    {
        var directory = GetCompletedSessionsDirectory();
        if (!Directory.Exists(directory))
        {
            return Array.Empty<PendingCompletedSession>();
        }

        var records = new List<PendingCompletedSession>();
        foreach (var filePath in Directory.GetFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(filePath);
            var record = await JsonSerializer.DeserializeAsync<PendingCompletedSession>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);

            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records
            .OrderBy(record => record.SavedUtc)
            .ThenBy(record => record.SessionId, StringComparer.Ordinal)
            .ToArray();
    }

    public Task<bool> RemoveCompletedSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var path = GetCompletedSessionPath(sessionId);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    private async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tempPath = $"{path}.{Guid.NewGuid():n}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private string GetCurrentSessionPath() =>
        Path.Combine(_options.RootDirectory, _options.CurrentSessionFileName);

    private string GetCompletedSessionsDirectory() =>
        Path.Combine(_options.RootDirectory, _options.CompletedSessionsDirectoryName);

    private string GetCompletedSessionPath(string sessionId) =>
        Path.Combine(GetCompletedSessionsDirectory(), $"{sessionId}.json");
}
