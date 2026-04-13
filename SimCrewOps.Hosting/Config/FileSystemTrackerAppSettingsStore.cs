using System.Text.Json;
using System.Text.Json.Serialization;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.Hosting.Config;

public sealed class FileSystemTrackerAppSettingsStore : ITrackerAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    private readonly FileSystemTrackerAppSettingsStoreOptions _options;

    public FileSystemTrackerAppSettingsStore(FileSystemTrackerAppSettingsStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SettingsFilePath);

        _options = options;
    }

    public async Task<TrackerAppSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_options.SettingsFilePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_options.SettingsFilePath);
        return await JsonSerializer.DeserializeAsync<TrackerAppSettings>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(TrackerAppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_options.SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_options.SettingsFilePath}.{Guid.NewGuid():n}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _options.SettingsFilePath, overwrite: true);
    }
}
