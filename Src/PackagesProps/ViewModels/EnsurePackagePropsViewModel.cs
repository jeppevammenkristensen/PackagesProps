using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackagesProps.Infrastructure;
using TruePath;

namespace PackagesProps.ViewModels;

public partial class EnsurePackagePropsViewModel(
    ILogger<EnsurePackagePropsViewModel> logger, 
    PackagePropsService packagePropsService) : ScreenPage
{
    public override string Title => "Ensure Package.props";

    [ObservableProperty] public partial AbsolutePath? SelectedFolder { get; set; }

    public async Task OnFolderSelectedAsync(AbsolutePath path)
    {
        SelectedFolder = path;
        logger.LogInformation("Folder selected: {Path}", path);
        await Task.CompletedTask;
    }

    private bool CanExecuteCreate()
    {
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCreate))]
    public async Task Create(CancellationToken token = default)
    {
        await packagePropsService.CreateProps(SelectedFolder!.Value, token);
        SetStatusMessage($"Created packages props");
    }
}