using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Presenter.Views;

internal sealed partial class MessageWindow : Window
{
    public MessageWindow(string title, string message, bool confirmChanges)
    {
        InitializeComponent();

        Title = title;
        Heading.Text = title;
        Message.Text = message;
        if (!confirmChanges)
        {
            CancelButton.IsVisible = false;
            IgnoreButton.IsVisible = false;
            AcceptButton.Content = "Fermer";
        }
    }

    private void OnChoice(object? sender, RoutedEventArgs args)
    {
        if (sender is Button button)
            Close(button.Content);
    }
}
