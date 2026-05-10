using System.Collections.Generic;
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

public partial class PackageAggregateViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Package { get; set; }
    
    [ObservableProperty] public partial string? PackagePropsVersion { get; set; }

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

        if (Package == "FluentAssertions")
        {
            int i = 0;
        }
        
        LatestVersions = [..versions.OrderByDescending(x => x.NugetVersion)];
        HighestAvailableVersion = LatestVersions.First();
        UsedVersion = PackagePropsVersion ?? HighestAvailableVersion;
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