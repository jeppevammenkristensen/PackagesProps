using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using FileBasedApp.Toolkit;
using FileBasedApp.Toolkit.CSharp;
using NuGet.Versioning;
using PackagesProps.Models;
using PackagesProps.ViewModels;
using TruePath;

namespace PackagesProps.Infrastructure;

public class ProjectAnalyser(
    IFileSystem fileSystem, 
    IServiceLocator locator) : IProjectAnalyser
{
    private string PackagePropsVersion { get; set; }

    public async IAsyncEnumerable<ProjectWrapper> AnalyzePathForProjectWrappers(AbsolutePath path)
    {
        path.ThrowIfNotDirectory();
        
        foreach (var enumerateAllFile in path.EnumerateAllFiles("*.csproj", fileSystem)
                     .Select(x => new ProjectWrapper(x)))
        {
            await enumerateAllFile.Load();
            yield return enumerateAllFile;
        }
    }
    
    public async IAsyncEnumerable<PackageAggregate> GetPackageReferences(IEnumerable<ProjectWrapper> projects, DirectoryPackagesPropsWrapper? packagesPropsWrapper)
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
            .GroupBy(x => x.Name);
        foreach (var v in groupBy)
        {
            if (packagesProps.TryGetValue(v.Key, out var packageProps) && packageProps.HasVersion)
            {
                PackagePropsVersion = packageProps.Version!;
            }
            
            var highestInstalledVersion = v
                .Where(x => x.HasVersion)
                .Select(x => new PackageVersion(x.Version!))
                .Where(x => x.Type == PackageVersionType.SemVer)
                .Max();
            
            yield return new PackageAggregate(
                v.Key,
                highestInstalledVersion?.ToString(),
                PackagePropsVersion);
            
        }
    }
}