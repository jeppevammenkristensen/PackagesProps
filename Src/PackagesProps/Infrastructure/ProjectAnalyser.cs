using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        Dictionary<string, PackageVersionGroup> packagesProps = packagesPropsWrapper?
            .GetPackageVersions()
            .Where(x => x.HasVersion)
            .GroupBy(x => x.PackageName)
            .ToDictionary(x => x.Key, x => new PackageVersionGroup([..x])) ?? [];
            

        foreach (var project in projects)
        {
            packageReferences.AddRange(project.GetAllPackageReferences());
        }
        
        var groupBy = packageReferences
            .GroupBy(x => x.Name);
        
        foreach (var v in groupBy)
        {
            if (packagesProps.TryGetValue(v.Key, out var packageProps) && !packageProps.IsEmpty)
            {
                PackagePropsVersion = packageProps.GetRequiredBestMatch.Version!;
            }
            
            var highestInstalledVersion = v
                .Where(x => x.HasVersion)
                .Select(x => new PackageVersion(x.Version!))
                .Where(x => x.Type == PackageVersionType.SemVer)
                .MaxBy(x => x.NugetVersion!);

            yield return new PackageAggregate(
                v.Key,
                PackagePropsVersion, highestInstalledVersion?.ToString(), packageProps?.Versions.Select(x => x.Version).ToImmutableArray() ?? []);
        }
    }
}

public record PackageVersionGroup(ImmutableArray<PackageVersionItem> Versions)
{
    public bool IsEmpty => !Versions.Any();
    
    public PackageVersionItem GetRequiredBestMatch => Versions
        .OrderBy(x => x.HasUpdate ? 0 : 1)
        .ThenBy(x => x.HasInclude ? 0 : 1)
        .FirstOrDefault() ?? throw new InvalidOperationException("Expected there to be at least one version in the group");
    
}