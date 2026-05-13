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
public class ProjectWrapper(AbsolutePath path, IFileSystem fileSystem)
{
    private XElement? _xml;


    public ProjectWrapper(AbsolutePath path) : this(path, new FileSystem())
    {
    }


    public AbsolutePath Path => path;

    public IEnumerable<PackageReference> PackageReferences => GetAllPackageReferences();

    protected XElement Xml
    {

        get
        {

            if (_xml == null)
            {
                throw new InvalidOperationException(
                    "Xml has not been loaded. Remember to call the Load method before accessing data");
            }

            return _xml;
        }
    }
    
    public async Task Load()
    {
        try
        {
            await using var fileSystemStream = path.OpenRead(fileSystem);
            _xml= await XElement.LoadAsync(fileSystemStream, LoadOptions.PreserveWhitespace, CancellationToken.None);
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to load project {path}", e);
        }
    }
    
    public IEnumerable<PackageReference> GetAllPackageReferences()
    {
        foreach (var packageReference in Xml.Descendants("PackageReference"))
        {
            yield return new PackageReference(packageReference);
        }
    }

    public void RemoveVersion(ImmutableArray<string> packageNames)
    {
        var hashSet = packageNames.ToHashSet();
        
        foreach (var packageName in packageNames)
        {
            if (Xml.Descendants("PackageReference")
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

        await using var stream = path.FileCreate();
        await using var writer = XmlWriter.Create(stream, settings);
        await Xml.SaveAsync(writer, CancellationToken.None);
    }
}