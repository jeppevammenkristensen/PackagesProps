using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileBasedApp.Toolkit.CSharp;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using PackagesProps.Infrastructure;
using TruePath;

namespace PackagesProps.ViewModels;

public class PackageResult
{
    public PackageResult(string repoName, string packageName, ImmutableArray<PackageVersion> versions)
    {
        RepoName = repoName;
        PackageName = packageName;
        Versions = versions;
    }

    public string RepoName { get; private set; }
    public string PackageName { get; private set; }
    
    public ImmutableArray<PackageVersion> Versions { get; private set; }
}

public class NugetRepository : INugetRepository
{
    public async Task<ImmutableArray<PackageResult>> FindPackages(string packageName, bool includePrerelease,AbsolutePath rootFolder, CancellationToken token = default
    )
    {
        ImmutableArray<PackageResult> result = [];
        var settings = Settings.LoadDefaultSettings(root: rootFolder.Value);
        var sourceProvider = new PackageSourceProvider(settings);
        var repos = sourceProvider.LoadPackageSources()
            .Where(s => s.IsEnabled)
            .Select(s => Repository.Factory.GetCoreV3(s));
        
        foreach (var repo in repos)
        {
            var search = await repo.GetResourceAsync<PackageSearchResource>(token);
            var hits = (await search.SearchAsync(
                packageName,
                new SearchFilter(includePrerelease: includePrerelease),
                skip: 0, take: 1,
                NullLogger.Instance, token)).ToList();

            if (hits.SingleOrDefault() is { } hit)
            {
                ImmutableArray<PackageVersion> versions =
                    [..(await hit.GetVersionsAsync()).Select(x => new PackageVersion(x.Version.ToNormalizedString()))];
                result = result.Add(new PackageResult(repo.PackageSource.Name, packageName, versions));
            }
        }

        return result;

    }
}

public interface INugetRepository
{
    Task<ImmutableArray<PackageResult>> FindPackages(string packageName, bool includePrerelease,AbsolutePath rootFolder, CancellationToken token = default
    );
}

public partial class PackageAggregateViewModel(INugetRepository nugetRepository, IUiDispatcher uiDispatcher) : ViewModelBase
{
    [ObservableProperty] public partial string Package { get; set; }
    
    [ObservableProperty] public partial string? PackagePropsVersion { get; set; }

    [NotifyCanExecuteChangedFor(nameof(UseHighestCommand))] [ObservableProperty] public partial string? HighestProjectsVersion { get; set; }

    [NotifyCanExecuteChangedFor(nameof(UseSelectedCommand))] [ObservableProperty] public partial string? HighestAvailableVersion { get; set; }

    [ObservableProperty] public partial ObservableCollection<string> LatestVersions { get; set; } = [];
    
    [NotifyCanExecuteChangedFor(nameof(UseSelectedCommand))] [NotifyCanExecuteChangedFor(nameof(UseHighestCommand))] [ObservableProperty] public partial string? UsedVersion { get; set; }
    
    [ObservableProperty] public partial bool IgnoreUpdate { get; set; }
    

    /// <summary>
    /// Queries every enabled NuGet source configured for <paramref name="root"/> for available versions
    /// of <see cref="Package"/> and updates the version-related properties of this view model.
    /// </summary>
    /// <remarks>
    /// On completion:
    /// <list type="bullet">
    ///   <item><description><see cref="LatestVersions"/> contains every returned version, sorted descending.</description></item>
    ///   <item><description><see cref="HighestAvailableVersion"/> is set to the newest version returned.</description></item>
    ///   <item><description><see cref="UsedVersion"/> is set to <see cref="PackagePropsVersion"/> when one is already
    ///     declared in <c>Directory.Packages.props</c>; otherwise it falls back to <see cref="HighestAvailableVersion"/>.</description></item>
    /// </list>
    /// NuGet sources are loaded via <see cref="Settings.LoadDefaultSettings"/> using <paramref name="root"/>, so
    /// any per-repository <c>NuGet.config</c> chain is honored. Disabled sources are skipped.
    /// </remarks>
    /// <param name="includePrerelease">If <c>true</c>, prerelease versions are included in the search results.</param>
    /// <param name="root">The repository root used to resolve the NuGet configuration chain.</param>

    public async Task Refresh(bool includePrerelease, AbsolutePath root)
    {
        var packageResults = await nugetRepository.FindPackages(Package, includePrerelease, root);
        
        // The NuGet query above runs on a Parallel.ForEachAsync worker thread; the property
        // setters below fire CanExecuteChanged on bound Buttons (StyledProperty.VerifyAccess),
        // so they must run on the UI thread.
        var sortedVersions = packageResults.SelectMany(x => x.Versions)
            .DistinctBy(x => x.NugetVersion)
            .OrderByDescending(x => x.NugetVersion).ToList();
        
        await uiDispatcher.InvokeAsync(() =>
        {
            LatestVersions = [..sortedVersions];
            HighestAvailableVersion = LatestVersions.FirstOrDefault();
            UsedVersion = PackagePropsVersion ?? HighestProjectsVersion ?? HighestAvailableVersion;
        });
    }

    private bool CanExecuteUseHighest()
    {
        return !string.IsNullOrWhiteSpace(HighestProjectsVersion) ;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUseHighest))]
    public void UseHighest()
    {
        UsedVersion = HighestProjectsVersion;
    }

    private bool CanExecuteUseSelected()
    {
        return !string.IsNullOrWhiteSpace(HighestAvailableVersion) && UsedVersion != HighestAvailableVersion;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUseSelected))]
    public void UseSelected()
    {
        UsedVersion = HighestAvailableVersion;
    }
}