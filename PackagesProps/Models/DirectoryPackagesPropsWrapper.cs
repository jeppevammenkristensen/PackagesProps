using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TruePath;

namespace PackagesProps.Models;

/// <summary>
/// Loads a <c>Directory.Packages.props</c> file as an <see cref="XElement"/> and exposes the
/// <c>PackageVersion</c> entries it declares.
/// </summary>
public class DirectoryPackagesPropsWrapper
{
    private readonly AbsolutePath _path;
    private XElement _xml;

    public DirectoryPackagesPropsWrapper(AbsolutePath path)
    {
        _path = path;
    }

    public AbsolutePath Path => _path;

    public async Task Load()
    {
        try
        {
            await using var fileSystemStream = _path.OpenRead();
            _xml = await XElement.LoadAsync(fileSystemStream, LoadOptions.PreserveWhitespace, CancellationToken.None);
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to load Directory.Packages.props {_path}", e);
        }
    }

    public IEnumerable<PackageVersionItem> GetPackageVersions()
    {
        foreach (var packageVersion in _xml.Descendants("PackageVersion"))
        {
            yield return new PackageVersionItem(packageVersion);
        }
    }
}