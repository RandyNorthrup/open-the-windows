using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Windows;

namespace OpenTheWindows.Cli;

/// <summary>
/// Services the command tree needs. Production wiring uses the real Windows
/// adapters and the embedded catalogue; tests supply fakes.
/// </summary>
/// <param name="Doctor">Pre-flight checker.</param>
/// <param name="LoadCatalog">Loads the catalogue; the argument is an optional override directory.</param>
internal sealed record CliServices(DoctorService Doctor, Func<string?, CatalogLoadResult> LoadCatalog)
{
    /// <summary>Production services.</summary>
    public static CliServices CreateDefault()
        => new(
            new DoctorService(new WindowsOperatingSystemInfo(), new WindowsElevationContext()),
            directory => directory is null ? CatalogLoader.LoadBuiltIn() : CatalogLoader.LoadWithDirectory(directory));
}
