using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using PackagesProps.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileBasedApp.Toolkit;
using FileBasedApp.Toolkit.CSharp;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using TruePath;

namespace PackagesProps.ViewModels;

public record PackageUpdate(string PackageName, string Version);

public record UpdateDirectoryProps(AbsolutePath DirectoryProps, ImmutableArray<PackageUpdate> Updates);

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


public partial class PackagesUpdaterViewModel(
    ILogger<PackagesUpdaterViewModel> logger,
    IFileSystem fileSystem) : ScreenPage
{
    public override string Title => "Packages updater";

    [ObservableProperty] public partial AbsolutePath? SelectedFolder { get; set; }
    [ObservableProperty] public partial ObservableCollection<PackageAggregateViewModel> PackageAggregateViewModels { get; set; }
    [ObservableProperty] public partial bool Preview { get; set; }
    [ObservableProperty] public partial List<ProjectWrapper> Projects { get; set; }

    private bool CanExecuteApply()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteApply))]
    protected async Task Apply()
    {
        var updateOperations = new UpdateOperations();
        await updateOperations.UpdateDirectory(SelectedFolder!.Value / "Directory.Packages.props", [..PackageAggregateViewModels.Select(x => new PackageUpdate(x.Package, x.UsedVersion!))]);
        await updateOperations.UpdateVersion([..Projects], [..PackageAggregateViewModels.Select(x => x.Package)]);
    }
    

    public async Task OnFolderSelectedAsync(AbsolutePath path)
    {
        SelectedFolder = path;
        logger.LogInformation("Folder selected: {Path}", path);
        await ExecuteRefreshCommand.ExecuteAsync(null);
    }

    private bool CanExecuteExecuteRefresh()
    {
        return SelectedFolder is { } selectedFolder && selectedFolder.DirectoryExists(fileSystem);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteExecuteRefresh))]
    public async Task ExecuteRefresh(CancellationToken token = default)
    {
        SetStatusMessage("Refreshing packages...");
        var projectAnalyser = new ProjectAnalyser(fileSystem);
        Projects = await projectAnalyser.AnalyzePathForProjectWrappers(SelectedFolder!.Value).ToListAsync(token);
        PackageAggregateViewModels = [..await projectAnalyser.GetPackageReferences(Projects).ToListAsync(token)];
        await LoadNugetData(token); 
    }

    private bool CanExecuteLoadNugetData()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteLoadNugetData))]
    public async Task LoadNugetData(CancellationToken token = default)
    {
        if (SelectedFolder is not { } root || PackageAggregateViewModels is null)
            return;

        SetStatusMessage("Loading NuGet data...");

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8,
            CancellationToken = token,
        };

        await Parallel.ForEachAsync(PackageAggregateViewModels, options, async (vm, ct) =>
        {
            try
            {
                await vm.Refresh(includePrerelease: Preview, root);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh package {Package}", vm.Package);
            }
        });
    }
    
}

public static class ExceptionUtil
{
    public static void ThrowIfNotDirectory(this AbsolutePath path, IFileSystem? fileSystem = null,
        [CallerArgumentExpression(nameof(path))] string? caller = null)
    {
        if (path.DirectoryExists(fileSystem))
            return;

        throw new InvalidOperationException($"Path {path} is not a valid directory");
    }
}

public class ProjectAnalyser(IFileSystem fileSystem)
{
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
    
    public async IAsyncEnumerable<PackageAggregateViewModel> GetPackageReferences(IEnumerable<ProjectWrapper> projects)
    {
       List<PackageReference> packageReferences = new();

        foreach (var project in projects)
        {
            packageReferences.AddRange(project.GetAllPackageReferences());
        }

        var groupBy = packageReferences.Where(x => x.HasInclude && x.HasVersion).GroupBy(x => x.Name);
        foreach (var v in groupBy)
        {
            var highestInstalledVersion = v.Select(x => NuGetVersion.Parse(x.Version!)).Max();
            yield return new PackageAggregateViewModel
            {
                Package = v.Key,
                HighestInstalledVersion = highestInstalledVersion?.ToNormalizedString(),
                UsedVersion = highestInstalledVersion?.ToNormalizedString()
               
            };
        }
    }
}


public partial class PackageAggregateViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Package { get; set; }

    [ObservableProperty] public partial string? HighestInstalledVersion { get; set; }

    [ObservableProperty] public partial string? HighestAvailableVersion { get; set; }

    [ObservableProperty] public partial ObservableCollection<string> LatestVersions { get; set; } = [];

    
    public AbsolutePath? Root { get; set; }
    
    [ObservableProperty] public partial string? UsedVersion { get; set; }

    

    public async Task Refresh(bool includePrerelease, AbsolutePath root)
    {
        var settings = Settings.LoadDefaultSettings(root: root.Value);
        var sourceProvider = new PackageSourceProvider(settings);
        var repos = sourceProvider.LoadPackageSources()
            .Where(s => s.IsEnabled)
            .Select(s => Repository.Factory.GetCoreV3(s));

        using var cache = new SourceCacheContext();
        List<PackageVersion> versions = new ();
        
        foreach (var repo in repos)
        {
            var search = await repo.GetResourceAsync<PackageSearchResource>();
            var hits = (await search.SearchAsync(
                Package,
                new SearchFilter(includePrerelease: includePrerelease),
                skip: 0, take: 1,
                NullLogger.Instance, CancellationToken.None)).ToList();

            if (hits?.SingleOrDefault() is { } hit)
            {
                versions.AddRange((await hit.GetVersionsAsync()).Select(x => new PackageVersion(x.Version.ToNormalizedString())));
            }
        }

        LatestVersions = [..versions.OrderByDescending(x => x.NugetVersion)];
        HighestAvailableVersion = LatestVersions.First();
    }

    private bool CanExecuteUseHighest()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUseHighest))]
    public void UseHighest()
    {
        UsedVersion = HighestInstalledVersion;
    }

    private bool CanExecuteUseSelected()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUseSelected))]
    public void UseSelected()
    {
        UsedVersion = HighestAvailableVersion;
    }
}

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

public class PackageReference
{
    private readonly XElement _packageReference;

    public PackageReference(XElement packageReference)
    {
        _packageReference = packageReference;
    }

    public bool HasInclude => Include != null;
    public bool HasVersion => Version != null;

    public string Name => Include ?? "[UNKOWN]";

    public string? Include => _packageReference.Attribute("Include")?.Value;

    public string? Version => GetAttributeOrElement("Version");

    public string? VersionOverride => GetAttributeOrElement("VersionOverride");

    public string? IncludeAssets => GetAttributeOrElement("IncludeAssets");

    public string? ExcludeAssets => GetAttributeOrElement("ExcludeAssets");

    public string? PrivateAssets => GetAttributeOrElement("PrivateAssets");

    public string? Condition => _packageReference.Attribute("Condition")?.Value;

    public string? GeneratePathProperty => GetAttributeOrElement("GeneratePathProperty");

    public string? Aliases => GetAttributeOrElement("Aliases");

    public string? NoWarn => GetAttributeOrElement("NoWarn");

    private string? GetAttributeOrElement(string name)
    {
        var attribute = _packageReference.Attribute(name)?.Value;
        if (attribute != null)
            return attribute;

        var element = _packageReference.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        return element?.Value;
    }
}