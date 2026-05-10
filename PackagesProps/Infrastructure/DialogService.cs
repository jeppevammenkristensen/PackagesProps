using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PackagesProps.Views;
using PackagesProps.Views.Dialogs;

namespace PackagesProps.Infrastructure;

public interface IDialogService
{
    /// <summary>
    /// Show a yes/no confirmation dialog. Returns <c>true</c> if the user confirmed.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");

    /// <summary>
    /// Show a modal dialog hosting the view registered for <typeparamref name="TViewModel"/> via the
    /// <see cref="ViewLocator"/>. The dialog closes when the view model raises
    /// <see cref="IDialogViewModel{TResult}.RequestClose"/>; the supplied value is returned.
    /// If the user closes the window without raising the event, <c>default</c> is returned.
    /// </summary>
    Task<TResult?> ShowDialogAsync<TViewModel, TResult>(TViewModel viewModel)
        where TViewModel : IDialogViewModel<TResult>;
}

/// <summary>
/// Implemented by view models that can be shown as a modal dialog via
/// <see cref="IDialogService.ShowDialogAsync{TViewModel,TResult}"/>. Raise <see cref="RequestClose"/>
/// with the result the caller should observe; the host window will then close.
/// </summary>
public interface IDialogViewModel<TResult>
{
    event EventHandler<TResult>? RequestClose;
}

public class DialogService : IDialogService
{
    private readonly ViewLocator _viewLocator;

    public DialogService(ViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        if (!TryGetOwner(out var owner)) return false;

        var dialog = new ConfirmDialog();
        dialog.SetContent(title, message, confirmText, cancelText);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<TResult?> ShowDialogAsync<TViewModel, TResult>(TViewModel viewModel)
        where TViewModel : IDialogViewModel<TResult>
    {
        if (!TryGetOwner(out var owner)) return default;

        var content = _viewLocator.Build(viewModel);
        var window = new Window
        {
            Content = content,
            DataContext = viewModel,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            CanResize = false,
        };

        TResult? result = default;

        void OnRequestClose(object? sender, TResult value)
        {
            result = value;
            window.Close();
        }

        viewModel.RequestClose += OnRequestClose;
        try
        {
            await window.ShowDialog(owner);
        }
        finally
        {
            viewModel.RequestClose -= OnRequestClose;
        }

        return result;
    }

    private static bool TryGetOwner(out Window owner)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } main })
        {
            owner = main;
            return true;
        }

        owner = null!;
        return false;
    }
}
