using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PackagesProps.Models;
using TruePath;

namespace PackagesProps.Infrastructure;

public class UpdateOperations
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
    
    public async Task UpdateDirectory(AbsolutePath directoryProps, ImmutableArray<PackageUpdate> updates)
    {
        XElement root = XElement.Load(directoryProps.Value);
        foreach (var packageUpdate in updates)
        {
            AddOrUpdate(root, packageUpdate.PackageName, packageUpdate.Version);
        }
        
        await using (var fileSystemStream = directoryProps.FileCreate())
        {
            await root.SaveAsync(fileSystemStream, SaveOptions.None, CancellationToken.None);    
        }
    }

    private void AddOrUpdate(XElement root, string packageUpdatePackageName, string packageUpdateVersion)
    {
        if (root.Descendants("PackageVersion").FirstOrDefault(x => (string?)x.Attribute("Include") == packageUpdatePackageName) is { } existingPackageVersion)
        {
            existingPackageVersion.SetAttributeValue("Version", packageUpdateVersion);
        }
        else
        {
            XElement itemGroup = default; 
            
            if (root.Element("ItemGroup") is {} item)
            {
                itemGroup = item;
            }
            else
            {
                itemGroup = new XElement("ItemGroup");
                root.Add(itemGroup);
            }
            
            itemGroup.Add(new XElement("PackageVersion", new XAttribute("Include", packageUpdatePackageName), new XAttribute("Version", packageUpdateVersion)));
        }
    }
}