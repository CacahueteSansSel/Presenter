using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Presenter.Views;

namespace Presenter;

internal sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow window = new();
            desktop.MainWindow = window;

            if (desktop.Args?.FirstOrDefault() is string path && File.Exists(path))
                window.Opened += async (_, _) => await window.OpenPathAsync(path);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
