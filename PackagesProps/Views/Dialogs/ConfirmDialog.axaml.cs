using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PackagesProps.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public void SetContent(string title, string message, string confirmText, string cancelText)
    {
        Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
    }

    private void Confirm_OnClick(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(false);
}
