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
using FileBasedApp.Toolkit;
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
    IDialogService dialogService,
    PackagePropsService packagePropsService,
    IFileSystem fileSystem) : ScreenPage
{
    public override string Title => "Packages updater";

    [NotifyPropertyChangedFor(nameof(TraverseUpVisible))] [NotifyCanExecuteChangedFor(nameof(TraverseUpCommand))] [ObservableProperty] public partial AbsolutePath? SelectedFolder { get; set; }
    [ObservableProperty] public partial ObservableCollection<PackageAggregateViewModel> PackageAggregateViewModels { get; set; }
    [ObservableProperty] public partial bool Preview { get; set; }
    [ObservableProperty] public partial List<ProjectWrapper> Projects { get; set; }
    [NotifyPropertyChangedFor(nameof(TraverseUpVisible))] [NotifyCanExecuteChangedFor(nameof(TraverseUpCommand))] [ObservableProperty] public partial bool HasDirectoryPackagesProps { get; set; }
    [ObservableProperty] public partial DirectoryPackagesPropsWrapper? DirectoryPackagesPropsWrapper { get; set; }

    public bool TraverseUpVisible => !HasDirectoryPackagesProps && SelectedFolder is not null; 
    
    

    private bool CanExecuteApply()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteApply))]
    protected async Task Apply()
    {
        if (SelectedFolder is not { } folder) return;

        var directoryPropsPath = folder / "Directory.Packages.props";

        if (!HasDirectoryPackagesProps)
        {
            var confirmed = await dialogService.ConfirmAsync(
                title: "Create Directory.Packages.props?",
                message:
                $"No Directory.Packages.props was found in {folder}. Create one before applying the updates?",
                confirmText: "Create",
                cancelText: "Cancel");

            if (!confirmed)
            {
                SetStatusMessage("Apply cancelled: Directory.Packages.props is missing.");
                return;
            }

            SetStatusMessage($"Creating {directoryPropsPath}...");
            await packagePropsService.CreateProps(folder);
            DirectoryPackagesPropsWrapper = new DirectoryPackagesPropsWrapper(directoryPropsPath);
            await DirectoryPackagesPropsWrapper.Load();
            HasDirectoryPackagesProps = true;
        }

        SetStatusMessage($"Starting update of {directoryPropsPath}...");
        var updateOperations = new UpdateOperations();

        var packagesToUpdate = PackageAggregateViewModels.Where(x => !x.IgnoreUpdate).ToList();

        await updateOperations.UpdateDirectoryPackagePropsFile(directoryPropsPath, [..packagesToUpdate.Select(x => new PackageUpdate(x.Package, x.UsedVersion!))]);
        await updateOperations.UpdateVersion([..Projects], [..packagesToUpdate.Select(x => x.Package)]);
        //await ExecuteRefreshCommand.ExecuteAsync(null);

        var directoryPackagesPropsWrapper = locator.GetRequiredService<DirectoryPackagesPropsViewerViewModel>();
        directoryPackagesPropsWrapper.FilePath = directoryPropsPath;
        await pageHost.AddPage(directoryPackagesPropsWrapper, false);
    }


    [RelayCommand]
    private void UseHighestForSelected(System.Collections.IList? items)
    {
        if (items is null) return;
        foreach (var vm in items.OfType<PackageAggregateViewModel>())
        {
            vm.UseHighest();
        }
    }

    [RelayCommand]
    private void UseSelectedForSelected(System.Collections.IList? items)
    {
        if (items is null) return;
        foreach (var vm in items.OfType<PackageAggregateViewModel>())
        {
            vm.UseSelected();
        }
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

    protected async Task CreateProps()
    {
        if (!HasDirectoryPackagesProps && SelectedFolder is not null)
        {
            await packagePropsService.CreateProps(SelectedFolder.Value);
        }
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


    private bool CanExecuteTraverseUp()
    {
        return TraverseUpVisible;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteTraverseUp))]
    protected async Task TraverseUp(CancellationToken token = default)
    {
        var findParentOrNull = this.SelectedFolder!.Value.FindParentOrNull(x => (x / "Directory.Packages.props").FileExists(fileSystem));
        if (findParentOrNull is { Value: {}})
        {
            SetStatusMessage($"Found Directory.Packages.props in {findParentOrNull.Value.Value}. Loading...");
            await OnFolderSelectedAsync(findParentOrNull.Value);
        }
        findParentOrNull = SelectedFolder!.Value.FindParentOrNull(x => x.GetFiles("*.sln*").Any());
        if (findParentOrNull is { Value: {}})
        {
            SetStatusMessage($"Found solution file in {findParentOrNull.Value.Value}. Setting this as folder");
            await OnFolderSelectedAsync(findParentOrNull.Value);
        }
    }
}