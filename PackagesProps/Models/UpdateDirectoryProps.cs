using System.Collections.Immutable;
using TruePath;

namespace PackagesProps.Models;

/// <summary>
/// A batch of <see cref="PackageUpdate"/>s to be applied to the
/// <c>Directory.Packages.props</c> file at <paramref name="DirectoryProps"/>.
/// </summary>
public record UpdateDirectoryProps(AbsolutePath DirectoryProps, ImmutableArray<PackageUpdate> Updates);