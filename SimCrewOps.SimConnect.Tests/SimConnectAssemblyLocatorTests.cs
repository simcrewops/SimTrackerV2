using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class SimConnectAssemblyLocatorTests
{
    [Fact]
    public void LocateManagedAssemblyPath_PrefersExplicitRootedFile()
    {
        var (directoryPath, filePath) = CreateManagedAssemblyFile();

        try
        {
            var locator = new SimConnectAssemblyLocator();
            var resolvedPath = locator.LocateManagedAssemblyPath(new SimConnectHostOptions
            {
                ManagedAssemblyPath = filePath,
            });

            Assert.Equal(Path.GetFullPath(filePath), resolvedPath);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(directoryPath);
        }
    }

    [Fact]
    public void LocateManagedAssemblyPath_FindsManagedWrapperInSearchDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, SimConnectAssemblyLocator.ManagedAssemblyFileName);
        File.WriteAllText(filePath, "placeholder");

        try
        {
            var locator = new SimConnectAssemblyLocator();
            var resolvedPath = locator.LocateManagedAssemblyPath(new SimConnectHostOptions
            {
                ManagedAssemblySearchPaths = [directoryPath],
            });

            Assert.Equal(Path.GetFullPath(filePath), resolvedPath);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(directoryPath);
        }
    }

    private static (string DirectoryPath, string FilePath) CreateManagedAssemblyFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, SimConnectAssemblyLocator.ManagedAssemblyFileName);
        File.WriteAllText(filePath, "placeholder");
        return (directoryPath, filePath);
    }
}
