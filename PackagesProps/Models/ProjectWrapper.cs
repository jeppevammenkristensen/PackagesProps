using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TruePath;

namespace PackagesProps.Models;

/// <summary>
/// Loads a <c>.csproj</c> file as an <see cref="XElement"/> and exposes operations for reading
/// <c>PackageReference</c> entries and stripping their <c>Version</c> attributes (for migration
/// to central package management).
/// </summary>
public class ProjectWrapper
{
    private readonly AbsolutePath _path;
    private XElement _xml;

    public ProjectWrapper(AbsolutePath path)
    {
        _path = path;
    }

    public async Task Load()
    {
        try
        {
            await using (var fileSystemStream = _path.OpenRead())
            {
                _xml= await XElement.LoadAsync(fileSystemStream, LoadOptions.PreserveWhitespace, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to load project {_path}", e);
        }
    }
    
    public IEnumerable<PackageReference> GetAllPackageReferences()
    {
        foreach (var packageReference in _xml.Descendants("PackageReference"))
        {
            yield return new PackageReference(packageReference);
        }
    }

    public void RemoveVersion(ImmutableArray<string> packageNames)
    {
        var hashSet = packageNames.ToHashSet();
        
        foreach (var packageName in packageNames)
        {
            if (_xml.Descendants("PackageReference")
                    .FirstOrDefault(x => (string?)x.Attribute("Include") == packageName) is { } match)
            {
                match.Attribute("Version")?.Remove();
            }    
        }

        
    }

    public async Task Save()
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            Async = true,
            // csproj files use UTF-8 with no BOM
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineHandling = NewLineHandling.None,
            NewLineChars = "\n",       // optional, match the dotnet SDK style
        };

        await using var stream = _path.FileCreate();
        await using var writer = XmlWriter.Create(stream, settings);
        await _xml.SaveAsync(writer, CancellationToken.None);
    }
}