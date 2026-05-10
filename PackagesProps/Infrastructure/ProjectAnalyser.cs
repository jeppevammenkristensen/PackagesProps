using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using FileBasedApp.Toolkit;
using NuGet.Versioning;
using PackagesProps.Models;
using PackagesProps.ViewModels;
using TruePath;

namespace PackagesProps.Infrastructure;

public class ProjectAnalyser(IFileSystem fileSystem)
{
    public string PackagePropsVersion { get; set; }

    public async IAsyncEnumerable<ProjectWrapper> AnalyzePathForProjectWrappers(AbsolutePath path)
    {
        path.ThrowIfNotDirectory();
        
        foreach (var enumerateAllFile in path.EnumerateAllFiles("*.csproj").Select(x => new ProjectWrapper(x)))
        {
            await enumerateAllFile.Load();
            yield return enumerateAllFile;
            //packageReferences.AddRange(enumerateAllFile.GetAllPackageReferences());
        }
    }
    
    public async IAsyncEnumerable<PackageAggregateViewModel> GetPackageReferences(IEnumerable<ProjectWrapper> projects, DirectoryPackagesPropsWrapper? packagesPropsWrapper)
    {
        List<PackageReference> packageReferences = new();
        Dictionary<string, PackageVersionItem> packagesProps = packagesPropsWrapper?.GetPackageVersions()
            .Where(x => x.HasInclude)
            .ToDictionary(x => x.Name) ?? [];

        foreach (var project in projects)
        {
            packageReferences.AddRange(project.GetAllPackageReferences());
        }

        var groupBy = packageReferences
            //.Where(x => x.HasInclude && x.HasVersion)
            .GroupBy(x => x.Name);
        foreach (var v in groupBy)
        {
            if (packagesProps.TryGetValue(v.Key, out var packageProps) && packageProps.HasVersion)
            {
                PackagePropsVersion = packageProps.Version!;
            }
            
            var highestInstalledVersion = v
                .Where(v => v.HasVersion)
                .Select(x => NuGetVersion.Parse(x.Version!)).Max();
            yield return new PackageAggregateViewModel
            {
                Package = v.Key,
                PackagePropsVersion = PackagePropsVersion,
                HighestInstalledVersion = highestInstalledVersion?.ToNormalizedString(),
                UsedVersion = highestInstalledVersion?.ToNormalizedString()
            };
        }
    }
}