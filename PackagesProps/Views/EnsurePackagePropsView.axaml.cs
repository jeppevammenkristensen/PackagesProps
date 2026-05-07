using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PackagesProps.ViewModels;
using TruePath;

namespace PackagesProps.Views;

public partial class EnsurePackagePropsView : UserControl
{
    public EnsurePackagePropsView()
    {
        InitializeComponent();
    }

    private async void PickFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EnsurePackagePropsViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        var picked = folders[0];
        var path = picked.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Directory.Exists(path)) return;

        await vm.OnFolderSelectedAsync(new AbsolutePath(path));
    }
}
