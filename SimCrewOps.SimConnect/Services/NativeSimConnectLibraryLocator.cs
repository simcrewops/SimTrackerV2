using System.Runtime.InteropServices;
using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public class NativeSimConnectLibraryLocator
{
    internal const string NativeLibraryName = "SimConnect";
    internal const string NativeLibraryFileName = NativeLibraryName + ".dll";
    private const string NativeLibraryEnvironmentVariable = "SIMCONNECT_NATIVE_DLL_PATH";

    public virtual nint LoadNativeLibrary(SimConnectHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (NativeLibrary.TryLoad(NativeLibraryName, out var handle) ||
            NativeLibrary.TryLoad(NativeLibraryFileName, out handle))
        {
            return handle;
        }

        var libraryPath = LocateNativeLibraryPath(options)
            ?? throw new DllNotFoundException(
                $"Unable to locate {NativeLibraryFileName}. Set {NativeLibraryEnvironmentVariable}, provide SimConnectHostOptions.NativeLibraryPath, or bundle the native SimConnect client library next to the app.");

        return NativeLibrary.Load(libraryPath);
    }

    internal virtual string? LocateNativeLibraryPath(SimConnectHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var candidate in GetSearchCandidates(options))
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Path.IsPathRooted(candidate))
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                var rootedCandidate = Path.Combine(candidate, NativeLibraryFileName);
                if (File.Exists(rootedCandidate))
                {
                    return Path.GetFullPath(rootedCandidate);
                }
            }
            else
            {
                var relativePath = Path.GetFullPath(candidate, AppContext.BaseDirectory);
                if (File.Exists(relativePath))
                {
                    return relativePath;
                }

                var relativeDirectoryCandidate = Path.Combine(relativePath, NativeLibraryFileName);
                if (File.Exists(relativeDirectoryCandidate))
                {
                    return relativeDirectoryCandidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string?> GetSearchCandidates(SimConnectHostOptions options)
    {
        yield return options.NativeLibraryPath;

        var environmentPath = Environment.GetEnvironmentVariable(NativeLibraryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            yield return environmentPath;
        }

        yield return AppContext.BaseDirectory;

        foreach (var searchPath in options.NativeLibrarySearchPaths)
        {
            yield return searchPath;
        }
    }
}
