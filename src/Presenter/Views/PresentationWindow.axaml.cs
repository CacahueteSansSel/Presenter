using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Presenter.Documents;
using Presenter.Flow;

namespace Presenter.Views;

internal sealed partial class PresentationWindow : Window
{
    private readonly IReadOnlyList<Slide> _slides;
    private readonly SlideTheme _theme;
    private readonly SlideImages _images;
    private SlideFlow? _flow;
    private int _index;
    private int _step;
    private bool _busy;
    private bool _closed;
    private bool _replaying;

    public PresentationWindow(Deck deck, int start, SlideTheme theme, SlideImages images)
    {
        _slides = deck.Slides;
        _index = start;
        _theme = theme;
        _images = images;

        InitializeComponent();
        ProjectionSurface.Interactive = false;
        Title = deck.Name;
        if (OperatingSystem.IsMacOS())
        {
            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = true;
        }

        Opened += (_, _) => Dispatcher.UIThread.Post(BeginPresentation, DispatcherPriority.Background);
        KeyDown += OnKey;
        PointerPressed += async (_, args) =>
        {
            if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                await RunAsync(ForwardAsync);
        };
        Closed += (_, _) =>
        {
            _closed = true;
            _flow?.Cancel();
            _images.Dispose();
            if (!_busy)
                ReleaseFlow();
        };
    }

    private async void BeginPresentation()
    {
        if (_closed)
            return;

        WindowState = WindowState.FullScreen;
        await RunAsync(() => EnterAsync(_index));
    }

    private async void OnKey(object? sender, KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Escape:
                if (_busy)
                    Close();
                else
                    await RunAsync(ExitAsync);
                break;
            case Key.Space:
            case Key.Enter:
            case Key.Right:
            case Key.Down:
            case Key.PageDown:
                await RunAsync(ForwardAsync);
                break;
            case Key.Left:
            case Key.Up:
            case Key.PageUp:
                await RunAsync(BackwardAsync);
                break;
            case Key.Home:
                await RunAsync(() => JumpAsync(0));
                break;
            case Key.End:
                await RunAsync(() => JumpAsync(_slides.Count - 1));
                break;
            default:
                return;
        }

        args.Handled = true;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy || _closed)
            return;

        _busy = true;
        try
        {
            await action();
        }
        catch (Exception exception) when (!_closed)
        {
            ShowError(exception);
        }
        catch (Exception) when (_closed)
        {
            // Cancellation can surface as a rejected script promise.
        }
        finally
        {
            _busy = false;
            if (_closed)
                ReleaseFlow();
        }
    }

    private async Task EnterAsync(int index, int replayParts = 0)
    {
        ErrorPanel.IsVisible = false;
        _index = index;
        _step = 0;
        ProjectionSurface.Load(_slides[index], _theme, _images.Resolve, animate: true);

        _replaying = replayParts != 0;
        try
        {
            _flow = new SlideFlow(_slides[index].Script, FadeAsync, Set, () => _replaying);
            await Task.Run(_flow.StartAsync);

            int target = Math.Min(replayParts, _flow.PartCount);
            while (_step < target)
                await PlayStepAsync();
        }
        finally
        {
            _replaying = false;
        }
    }

    private Task PlayStepAsync()
    {
        if (_flow is not SlideFlow flow)
            return Task.CompletedTask;

        int index = _step++;

        return Task.Run(() => flow.StepAsync(index));
    }

    private async Task ForwardAsync()
    {
        ErrorPanel.IsVisible = false;
        if (_flow != null && _step < _flow.PartCount)
        {
            await PlayStepAsync();

            return;
        }

        await FinishAsync();
        if (_closed)
            return;

        if (_index + 1 == _slides.Count)
            Close();
        else
            await EnterAsync(_index + 1);
    }

    private async Task BackwardAsync()
    {
        if (_step > 0)
        {
            int target = _step - 1;
            ReleaseFlow();
            await EnterAsync(_index, target);

            return;
        }

        if (_index == 0)
            return;

        await FinishAsync();
        if (!_closed)
            await EnterAsync(_index - 1, int.MaxValue);
    }

    private async Task JumpAsync(int index)
    {
        if (_index == index)
            return;

        await FinishAsync();
        if (!_closed)
            await EnterAsync(index);
    }

    private async Task ExitAsync()
    {
        await FinishAsync();
        if (!_closed)
            Close();
    }

    private async Task FinishAsync()
    {
        if (_flow == null)
            return;

        try
        {
            await Task.Run(_flow.FinishAsync);
        }
        catch (Exception exception) when (!_closed)
        {
            ShowError(exception);
            await Task.Delay(950);
        }
        finally
        {
            ReleaseFlow();
        }
    }

    private void ReleaseFlow()
    {
        _flow?.Dispose();
        _flow = null;
    }

    private void ShowError(Exception exception)
    {
        ErrorMessage.Text = $"Code Flow · {_slides[_index].Name} · {exception.Message}";
        ErrorPanel.IsVisible = true;
    }

    private async Task FadeAsync(string id, double opacity, int duration, CancellationToken cancellationToken)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            ProjectionSurface.FadeAsync(id, opacity, _replaying ? 0 : duration, cancellationToken));
    }

    private void Set(string operation, string id, string text, double x, double y) =>
        Dispatcher.UIThread.Invoke(() => ProjectionSurface.Apply(operation, id, text, x, y));
}
