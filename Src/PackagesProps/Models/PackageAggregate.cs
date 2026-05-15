using System.Collections.Immutable;

namespace PackagesProps.Models;

/// <summary>
/// DTO produced by <see cref="Infrastructure.ProjectAnalyser"/> that carries the per-package
/// data needed to populate a <c>PackageAggregateViewModel</c>. Keeping this as a plain
/// record lets the analyser stay free of any view-model / DI concerns and makes the
/// analyser output trivially testable.
/// </summary>
/// <param name="Package">The NuGet package id (e.g. <c>Newtonsoft.Json</c>).</param>
/// <param name="PackagePropsVersion">
/// The version declared for this package in <c>Directory.Packages.props</c>, or <c>null</c>
/// when the package is not pinned there.
/// </param>
/// <param name="HighestProjectsVersion">
/// The highest valid SemVer version found across the <c>PackageReference</c> entries of the
/// analysed projects, or <c>null</c> when none of the references carries a parseable version.
/// </param>
public record PackageAggregate(
    string Package,
    string? PackagePropsVersion,
    string? HighestProjectsVersion,
    ImmutableArray<string?> PackagePropsVersions)
{
    public string? UsedVersion => PackagePropsVersion ?? HighestProjectsVersion;
}
