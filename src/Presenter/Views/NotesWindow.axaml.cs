using Avalonia.Controls;
using Presenter.Documents;

namespace Presenter.Views;

internal sealed partial class NotesWindow : Window
{
    public NotesWindow(string slideName, string markdown)
    {
        InitializeComponent();
        Title = $"Notes · {slideName}";
        NotesScroll.Content = SlideText.MarkdownText(new SlideElement { FontSize = 16 }, markdown);
    }
}
