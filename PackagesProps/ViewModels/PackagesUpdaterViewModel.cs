using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PackagesProps.Infrastructure;
using PackagesProps.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using PackagesProps.Models.Messages;
using TruePath;
using IMessengerExtensions = CommunityToolkit.Mvvm.Messaging.IMessengerExtensions;

namespace PackagesProps.ViewModels;

public partial class PackagesUpdaterViewModel(
    ILogger<PackagesUpdaterViewModel> logger,
    IServiceLocator locator,
    IPageHost pageHost,
    IFileSystem fileSystem) : ScreenPage
{
    public override string Title => "Packages updater";

    [ObservableProperty] public partial AbsolutePath? SelectedFolder { get; set; }
    [ObservableProperty] public partial ObservableCollection<PackageAggregateViewModel> PackageAggregateViewModels { get; set; }
    [ObservableProperty] public partial bool Preview { get; set; }
    [ObservableProperty] public partial List<ProjectWrapper> Projects { get; set; }
    [ObservableProperty] public partial bool HasDirectoryPackagesProps { get; set; }
    [ObservableProperty] public partial DirectoryPackagesPropsWrapper DirectoryPackagesPropsWrapper { get; set; }

    private bool CanExecuteApply()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteApply))]
    protected async Task Apply()
    {
        SetStatusMessage($"Starting update of {SelectedFolder!.Value / "Directory.Packages.props"}...");
        var updateOperations = new UpdateOperations();
        await updateOperations.UpdateDirectory(SelectedFolder!.Value / "Directory.Packages.props", [..PackageAggregateViewModels.Select(x => new PackageUpdate(x.Package, x.UsedVersion!))]);
        await updateOperations.UpdateVersion([..Projects], [..PackageAggregateViewModels.Select(x => x.Package)]);
        //await ExecuteRefreshCommand.ExecuteAsync(null);

        var directoryPackagesPropsWrapper = locator.GetRequiredService<DirectoryPackagesPropsViewerViewModel>();
        directoryPackagesPropsWrapper.FilePath = SelectedFolder!.Value / "Directory.Packages.props";
        await pageHost.AddPage(directoryPackagesPropsWrapper, false);
    }
    

    public async Task OnFolderSelectedAsync(AbsolutePath path)
    {
        SelectedFolder = path;
        HasDirectoryPackagesProps = (path / "Directory.Packages.props").FileExists(fileSystem);

        if (HasDirectoryPackagesProps)
        {
            SetStatusMessage("Loading Directory.Packages.props...");
            DirectoryPackagesPropsWrapper = new DirectoryPackagesPropsWrapper(path / "Directory.Packages.props");    
            await DirectoryPackagesPropsWrapper.Load();
            SetStatusMessage("Loaded Directory.Packages.props...");
        }
        
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
        var projectAnalyser = new ProjectAnalyser(fileSystem);
        SetStatusMessage("Analysing projects...");
        Projects = await projectAnalyser.AnalyzePathForProjectWrappers(SelectedFolder!.Value).ToListAsync(token);
        SetStatusMessage("Checking package references...");
        PackageAggregateViewModels = [..await projectAnalyser.GetPackageReferences(Projects, DirectoryPackagesPropsWrapper).ToListAsync(token)];
        await LoadNugetData(token); 
        SetStatusMessage("Completed");
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