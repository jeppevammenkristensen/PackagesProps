using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PackagesProps.Models;
using TruePath;

namespace PackagesProps.Infrastructure;

public class UpdateOperations(IFileSystem fileSystem)
{
    public async Task UpdateVersion(ImmutableArray<ProjectWrapper> wrapper, ImmutableArray<string> packageNames)
    {
        foreach (var projectWrapper in wrapper)
        {
            if (!projectWrapper.GetAllPackageReferences().Any())
                continue;
            
            projectWrapper.RemoveVersion(packageNames);   
            await projectWrapper.Save();
        }
    }
    
    public async Task UpdateDirectoryPackagePropsFile(AbsolutePath directoryProps, ImmutableArray<PackageUpdate> updates)
    {
        XElement root;
        await using (var readStream = fileSystem.File.OpenRead(directoryProps.Value))
        {
            root = XElement.Load(readStream);
        }

        foreach (var packageUpdate in updates)
        {
            AddOrUpdate(root, packageUpdate.PackageName, packageUpdate.Version);
        }

        await using (var fileSystemStream = fileSystem.File.Create(directoryProps.Value))
        {
            await root.SaveAsync(fileSystemStream, SaveOptions.None, CancellationToken.None);    
        }
    }

    private void AddOrUpdate(XElement root, string packageUpdatePackageName, string packageUpdateVersion)
    {
        var packageVersionMatch = root.Descendants("PackageVersion")
            .Where(x => (string?) x.Attribute("Update") == packageUpdatePackageName ||
                        (string?) x.Attribute("Include") == packageUpdatePackageName).ToList();

        if (packageVersionMatch.Count > 0)
        {
            if (packageVersionMatch.Count == 1)
            {
                packageVersionMatch[0].SetAttributeValue("Version", packageUpdateVersion);
                return;
            }

            // If more than 1 match we will update the last version
            if (packageVersionMatch
                    .LastOrDefault(x => (string?) x.Attribute("Version") != null) is { } match)
            {
                match.SetAttributeValue("Version", packageUpdateVersion);
                return;
            }

        }

        XElement itemGroup;

        if (root.Element("ItemGroup") is { } item)
        {
            itemGroup = item;
        }
        else
        {
            itemGroup = new XElement("ItemGroup");
            root.Add(itemGroup);
        }

        itemGroup.Add(new XElement("PackageVersion", new XAttribute("Include", packageUpdatePackageName),
            new XAttribute("Version", packageUpdateVersion)));
    }
}