using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PackagesProps.Infrastructure;
using PackagesProps.Models;
using TruePath;

namespace PackagesProps.ViewModels;

/// <summary>
/// Displays the contents of a <c>Directory.Packages.props</c> file as read-only XML text.
/// Call <see cref="LoadAsync"/> with a <see cref="DirectoryPackagesPropsWrapper"/> to populate the view.
/// </summary>
public partial class DirectoryPackagesPropsViewerViewModel(ILogger<DirectoryPackagesPropsViewerViewModel> logger, IFileSystem fileSystem) : ScreenPage
{
    public override string Title => FilePath?.Value ?? "Unknown file";

    [NotifyPropertyChangedFor(nameof(Title))] [ObservableProperty] public partial AbsolutePath? FilePath { get; set; }

    [ObservableProperty] public partial string Content { get; set; } = string.Empty;

    public override async Task OnActivatedAsync()
    {
        await LoadAsync();
        await base.OnActivatedAsync();

    }

    public async Task LoadAsync(CancellationToken token = default)
    {
        if (FilePath == null)
        {
            logger.LogError($"The FilePath has not been set");
        }
        
        await using var stream = FilePath!.Value.OpenRead();
        var doc = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, token);
        Content = doc.ToString(SaveOptions.None);
    }
}
