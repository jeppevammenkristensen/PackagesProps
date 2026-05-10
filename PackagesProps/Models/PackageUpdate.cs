namespace PackagesProps.Models;

/// <summary>
/// Represents a request to set <paramref name="PackageName"/> to a specific <paramref name="Version"/>
/// in a <c>Directory.Packages.props</c> file.
/// </summary>
public record PackageUpdate(string PackageName, string Version);