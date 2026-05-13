using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using FileBasedApp.Toolkit;
using NuGet.Versioning;
using PackagesProps.Models;
using PackagesProps.ViewModels;
using TruePath;

namespace PackagesProps.Infrastructure;

public interface IProjectAnalyser
{
    IAsyncEnumerable<ProjectWrapper> AnalyzePathForProjectWrappers(AbsolutePath path);
    IAsyncEnumerable<PackageAggregateViewModel> GetPackageReferences(IEnumerable<ProjectWrapper> projects, DirectoryPackagesPropsWrapper? packagesPropsWrapper);
}

public class ProjectAnalyser(
    IFileSystem fileSystem, 
    IServiceLocator locator) : IProjectAnalyser
{
    private string PackagePropsVersion { get; set; }

    public async IAsyncEnumerable<ProjectWrapper> AnalyzePathForProjectWrappers(AbsolutePath path)
    {
        path.ThrowIfNotDirectory();
        
        foreach (var enumerateAllFile in path.EnumerateAllFiles("*.csproj", fileSystem).Select(x => new ProjectWrapper(x)))
        {
            await enumerateAllFile.Load();
            yield return enumerateAllFile;
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
            .GroupBy(x => x.Name);
        foreach (var v in groupBy)
        {
            if (packagesProps.TryGetValue(v.Key, out var packageProps) && packageProps.HasVersion)
            {
                PackagePropsVersion = packageProps.Version!;
            }
            
            var highestInstalledVersion = v
                .Where(x => x.HasVersion)
                .Select(x => NuGetVersion.Parse(x.Version!)).Max();


            var packageAggregateViewModel = locator.GetRequiredService<PackageAggregateViewModel>();
            packageAggregateViewModel.Package = v.Key;
            packageAggregateViewModel.PackagePropsVersion = PackagePropsVersion;
            packageAggregateViewModel.HighestProjectsVersion = highestInstalledVersion?.ToNormalizedString();
            packageAggregateViewModel.UsedVersion = highestInstalledVersion?.ToNormalizedString();

            yield return packageAggregateViewModel;
        }
    }
}