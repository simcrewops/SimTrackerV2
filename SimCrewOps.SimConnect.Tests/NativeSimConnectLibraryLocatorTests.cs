using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class NativeSimConnectLibraryLocatorTests
{
    [Fact]
    public void LocateNativeLibraryPath_PrefersExplicitRootedFile()
    {
        var (directoryPath, filePath) = CreateNativeLibraryFile();

        try
        {
            var locator = new NativeSimConnectLibraryLocator();
            var resolvedPath = locator.LocateNativeLibraryPath(new SimConnectHostOptions
            {
                NativeLibraryPath = filePath,
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
    public void LocateNativeLibraryPath_FindsNativeLibraryInSearchDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, NativeSimConnectLibraryLocator.NativeLibraryFileName);
        File.WriteAllText(filePath, "placeholder");

        try
        {
            var locator = new NativeSimConnectLibraryLocator();
            var resolvedPath = locator.LocateNativeLibraryPath(new SimConnectHostOptions
            {
                NativeLibrarySearchPaths = [directoryPath],
            });

            Assert.Equal(Path.GetFullPath(filePath), resolvedPath);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(directoryPath);
        }
    }

    private static (string DirectoryPath, string FilePath) CreateNativeLibraryFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, NativeSimConnectLibraryLocator.NativeLibraryFileName);
        File.WriteAllText(filePath, "placeholder");
        return (directoryPath, filePath);
    }
}
