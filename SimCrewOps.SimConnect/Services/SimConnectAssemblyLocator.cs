using System.Reflection;
using System.Runtime.Loader;
using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public class SimConnectAssemblyLocator
{
    internal const string ManagedAssemblyName = "Microsoft.FlightSimulator.SimConnect";
    internal const string ManagedAssemblyFileName = ManagedAssemblyName + ".dll";
    private const string ManagedAssemblyEnvironmentVariable = "SIMCONNECT_MANAGED_ASSEMBLY_PATH";

    public virtual Assembly LoadManagedAssembly(SimConnectHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return Assembly.Load(new AssemblyName(ManagedAssemblyName));
        }
        catch (FileNotFoundException)
        {
        }

        var assemblyPath = LocateManagedAssemblyPath(options)
            ?? throw new FileNotFoundException(
                $"Unable to locate {ManagedAssemblyFileName}. Set {ManagedAssemblyEnvironmentVariable}, provide SimConnectHostOptions.ManagedAssemblyPath, or bundle the managed wrapper next to the app.");

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    }

    internal virtual string? LocateManagedAssemblyPath(SimConnectHostOptions options)
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

                var rootedCandidate = Path.Combine(candidate, ManagedAssemblyFileName);
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

                var relativeDirectoryCandidate = Path.Combine(relativePath, ManagedAssemblyFileName);
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
        yield return options.ManagedAssemblyPath;

        var environmentPath = Environment.GetEnvironmentVariable(ManagedAssemblyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            yield return environmentPath;
        }

        yield return AppContext.BaseDirectory;

        foreach (var searchPath in options.ManagedAssemblySearchPaths)
        {
            yield return searchPath;
        }
    }
}
