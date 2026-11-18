using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Jint;
using Presenter.Documents;
using Presenter.Flow;

namespace Presenter.Views;

internal sealed partial class MainWindow : Window
{
    private readonly IReadOnlyList<SlideTheme> _themes = SlideTheme.Discover();
    private readonly SlideTheme _fallbackTheme = new() { Id = "signal", Name = "Signal" };
    private readonly DispatcherTimer _syntaxTimer = new() { Interval = TimeSpan.FromMilliseconds(310) };
    private readonly Dictionary<Guid, SlideSurface> _thumbnails = [];
    private Dictionary<string, byte[]> _fonts = new(StringComparer.Ordinal);
    private SlideImages _images = new([]);
    private PresentationWindow? _presentation;
    private Deck _deck = Templates.CreateDemo();
    private DeckHistory _history;
    private Guid _slideId;
    private string? _elementId;
    private string? _path;
    private string _savedSnapshot;
    private bool _loading;
    private bool _gizmo;
    private bool _allowClose;
    private bool _confirmingClose;
    private bool _fileBusy;
    private double _zoom = 1;

    private Slide CurrentSlide => _deck.Slides.FirstOrDefault(slide => slide.Id == _slideId) ?? _deck.Slides[0];
    private SlideElement? CurrentElement => CurrentSlide.Elements.FirstOrDefault(element => element.Id == _elementId);
    private SlideTheme CurrentTheme => _themes.FirstOrDefault(theme => theme.Id == _deck.Theme) ??
        _themes.FirstOrDefault() ?? _fallbackTheme;
    private int SlideIndex => _deck.Slides.IndexOf(CurrentSlide);
    private bool IsDirty => _history.Current != _savedSnapshot;

    public MainWindow()
    {
        _slideId = _deck.Slides[0].Id;
        _history = new DeckHistory(_deck, _slideId);
        _savedSnapshot = _history.Current;

        InitializeComponent();
        ConfigureChrome();
        ApiChips.ItemsSource = FlowApi.Methods.Where(method => method.Name != "beforePresent" && method.Name != "afterPresent").ToArray();
        ConnectInspector();

        ThemePicker.ItemsSource = _themes.Select(theme => theme.Name).ToArray();
        ThemePicker.SelectionChanged += OnThemeChanged;
        WorkArea.SizeChanged += (_, _) => ResizeStage();

        Surface.SelectionChanged += element => SelectElement(element?.Id);
        Surface.TransformCompleted += () =>
        {
            Record();
            BuildInspector();
            RefreshThumbnail();
        };
        Surface.EditRequested += element =>
        {
            SelectElement(element.Id);
            ShowTab(code: false);
        };

        CodeEditor.ElementIds = () => CurrentSlide.Elements.Select(element => element.Id);
        CodeEditor.TextChanged += (_, _) =>
        {
            if (_loading)
                return;

            CurrentSlide.Script = CodeEditor.Text;
            Record($"script:{_slideId}");
            _syntaxTimer.Stop();
            _syntaxTimer.Start();
        };
        _syntaxTimer.Tick += (_, _) =>
        {
            _syntaxTimer.Stop();
            CheckSyntax();
        };

        AddHandler(KeyDownEvent, OnShortcut, RoutingStrategies.Tunnel);
        Closing += OnWindowClosing;
        Closed += (_, _) =>
        {
            _syntaxTimer.Stop();
            _presentation?.Close();
            _images.Dispose();
        };
        Refresh();
    }

    private void ConfigureChrome()
    {
        bool macOS = OperatingSystem.IsMacOS();
        if (macOS)
        {
            WindowDecorations = WindowDecorations.Full;
            TopLeft.Margin = new Thickness(84, 0, 0, 0);
            WindowButtons.IsVisible = false;
        }

        FootHint.Text = macOS ? "⌘S  Enregistrer     F5  Présenter" : "Ctrl+S  Enregistrer     F5  Présenter";
    }

    private void OnPreviousSlide(object? sender, RoutedEventArgs args) => Navigate(-1);
    private void OnNextSlide(object? sender, RoutedEventArgs args) => Navigate(1);
    private void OnUndo(object? sender, RoutedEventArgs args) => ChangeHistory(false);
    private void OnRedo(object? sender, RoutedEventArgs args) => ChangeHistory(true);
    private void OnPresentDeck(object? sender, RoutedEventArgs args) => Present(fromBeginning: true);
    private void OnTestSlide(object? sender, RoutedEventArgs args) => Present(fromBeginning: false);
    private void OnMinimize(object? sender, RoutedEventArgs args) => WindowState = WindowState.Minimized;
    private void OnMaximize(object? sender, RoutedEventArgs args) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object? sender, RoutedEventArgs args) => Close();
    private async void OnNew(object? sender, RoutedEventArgs args) => await RunFileAsync(NewAsync);
    private async void OnOpen(object? sender, RoutedEventArgs args) => await RunFileAsync(ChooseOpenAsync);
    private async void OnSave(object? sender, RoutedEventArgs args) => await RunFileAsync(() => SaveAsync());
    private async void OnSaveAs(object? sender, RoutedEventArgs args) => await RunFileAsync(() => SaveAsync(saveAs: true));
    private async void OnOpenThemeFolder(object? sender, RoutedEventArgs args) => await OpenThemeFolderAsync();

    private void OnOpeningLayouts(object? sender, EventArgs args)
    {
        if (sender is MenuFlyout menu)
            BuildLayoutMenu(menu);
    }

    private void OnInsertText(object? sender, RoutedEventArgs args) => AddElement(ElementKind.Text, "Texte", 33);
    private void OnInsertTitle(object? sender, RoutedEventArgs args) => AddElement(ElementKind.Text, "Titre", 69, true);
    private void OnInsertSubtitle(object? sender, RoutedEventArgs args) => AddElement(ElementKind.Text, "Sous-titre", 41);
    private void OnInsertSection(object? sender, RoutedEventArgs args) => AddElement(ElementKind.Text, "RUBRIQUE", 25, true);
    private void OnInsertBullets(object? sender, RoutedEventArgs args) =>
        AddElement(ElementKind.Text, "- Premier point\n- Deuxième point", 33);
    private void OnInsertNumbers(object? sender, RoutedEventArgs args) =>
        AddElement(ElementKind.Text, "1. Premier point\n2. Deuxième point", 33);
    private void OnInsertImage(object? sender, RoutedEventArgs args) => RunImageImport();
    private void OnInsertCode(object? sender, RoutedEventArgs args) => AddElement(ElementKind.Code);
    private void OnInsertWindow(object? sender, RoutedEventArgs args) => AddElement(ElementKind.Window);

    private void OnClassicHandles(object? sender, RoutedEventArgs args) => SetGizmo(false);
    private void OnGizmo(object? sender, RoutedEventArgs args) => SetGizmo(true);
    private void OnDescriptionTab(object? sender, RoutedEventArgs args) => ShowTab(code: false);
    private void OnCodeTab(object? sender, RoutedEventArgs args) => ShowTab(code: true);
    private void OnZoomOut(object? sender, RoutedEventArgs args) => ChangeZoom(-.1);
    private void OnZoomIn(object? sender, RoutedEventArgs args) => ChangeZoom(.1);

    private void OnZoomReset(object? sender, RoutedEventArgs args)
    {
        _zoom = 1;
        ResizeStage();
    }

    private void OnInsertApiCall(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button button || button.DataContext is not FlowMethod method)
            return;

        CodeEditor.Focus();
        int position = CodeEditor.CaretOffset;
        CodeEditor.Document.Insert(position, method.Name + "()");
        CodeEditor.CaretOffset = position + method.Name.Length + 1;
    }

    private void Refresh()
    {
        _loading = true;
        try
        {
            Slide slide = CurrentSlide;
            _slideId = slide.Id;
            CodeEditor.DismissCompletion();
            if (CodeEditor.Text != slide.Script)
                CodeEditor.Text = slide.Script;

            ScriptName.Text = $"slide-{SlideIndex + 1:00}.js";
            CanvasTitle.Text = $"DIAPO {SlideIndex + 1:00}  /  {_deck.Slides.Count:00}";
            CanvasLayout.Text = CurrentTheme.GetLabel(slide.Layout) + " · " + CurrentTheme.Name;
            ThemePicker.SelectedIndex = _themes.ToList().IndexOf(CurrentTheme);

            Surface.Load(slide, CurrentTheme, _images.Resolve, animate: false);
            SelectElement(_elementId);
            BuildSlideList();
            UpdateStatus();
            CheckSyntax();
        }
        finally
        {
            _loading = false;
        }
    }

    private void BuildSlideList()
    {
        _thumbnails.Clear();
        SlideCount.Text = _deck.Slides.Count == 1 ? "01 DIAPO" : $"{_deck.Slides.Count:00} DIAPOS";

        SlideList.ItemsSource = _deck.Slides.Select((slide, index) =>
        {
            SlideSurface miniature = new(false);
            miniature.Load(slide, CurrentTheme, _images.Resolve, animate: false);
            _thumbnails[slide.Id] = miniature;

            return new SlideCard(slide.Id, $"{index + 1:00}", slide.Name, slide.Id == _slideId, miniature);
        }).ToArray();

        Dispatcher.UIThread.Post(() => SlideList.ContainerFromIndex(SlideIndex)?.BringIntoView(), DispatcherPriority.Background);
    }

    private void RefreshThumbnail()
    {
        if (_thumbnails.TryGetValue(_slideId, out SlideSurface? miniature))
            miniature.Load(CurrentSlide, CurrentTheme, _images.Resolve, animate: false);
    }

    private void BuildElementList()
    {
        ElementsList.ItemsSource = CurrentSlide.Elements.Select(element => new ElementChip(
            element.Id,
            element.Kind switch
            {
                ElementKind.Image => "image",
                ElementKind.Window => "browser",
                ElementKind.Code => "code-block",
                _ => "text"
            },
            element.Id == _elementId)).ToArray();
        EmptyElements.IsVisible = CurrentSlide.Elements.Count == 0;
    }

    private void OnSelectSlide(object? sender, RoutedEventArgs args)
    {
        if (sender is Control control && control.DataContext is SlideCard slide)
            OpenSlide(slide.Id);
    }

    private void OnSelectElement(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control control || control.DataContext is not ElementChip element)
            return;

        SelectElement(element.Id);
        ShowTab(code: false);
    }

    private void OnSlideMenu(object? sender, RoutedEventArgs args)
    {
        if (sender is not MenuItem item || item.DataContext is not SlideCard slide)
            return;

        OpenSlide(slide.Id);
        switch (item.Tag)
        {
            case "duplicate":
                DuplicateSlide();
                break;
            case "up":
                MoveSlide(-1);
                break;
            case "down":
                MoveSlide(1);
                break;
            case "delete":
                DeleteSlide();
                break;
        }
    }

    private void OnElementMenu(object? sender, RoutedEventArgs args)
    {
        if (sender is not MenuItem item || item.DataContext is not ElementChip chip)
            return;

        SelectElement(chip.Id);
        switch (item.Tag)
        {
            case "duplicate":
                DuplicateElement();
                break;
            case "front":
                ReorderElement(front: true);
                break;
            case "back":
                ReorderElement(front: false);
                break;
            case "delete":
                DeleteElement();
                break;
        }
    }

    private void SelectElement(string? id)
    {
        _elementId = CurrentSlide.Elements.Any(element => element.Id == id) ? id : null;
        Surface.Select(_elementId, _gizmo);
        BuildElementList();
        BuildInspector();
    }

    private void SetGizmo(bool enabled)
    {
        _gizmo = enabled;
        GizmoButton.Classes.Set("active", enabled);
        FrameButton.Classes.Set("active", !enabled);
        Surface.Select(_elementId, _gizmo);
    }

    private void ShowTab(bool code)
    {
        CodePanel.IsVisible = code;
        DescriptionPanel.IsVisible = !code;
        CodeTab.Classes.Set("active", code);
        DescriptionTab.Classes.Set("active", !code);

        if (code)
            CheckSyntax();
        else
            CodeEditor.DismissCompletion();
    }

    private void ChangeZoom(double amount)
    {
        _zoom = Math.Clamp(Math.Round(_zoom + amount, 1), .45, 1.35);
        ResizeStage();
    }

    private void ResizeStage()
    {
        double width = Math.Min(Math.Max(0, WorkArea.Bounds.Width - 50),
            Math.Max(0, WorkArea.Bounds.Height - 103) * (16d / 9));
        StageFrame.Width = width * _zoom;
        StageFrame.Height = StageFrame.Width * 9 / 16;
        ZoomLabel.Text = _zoom == 1 ? "Auto" : $"{_zoom:P0}";
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs args)
    {
        int index = ThemePicker.SelectedIndex;
        if (_loading || index < 0 || index >= _themes.Count)
            return;

        _deck.Theme = _themes[index].Id;
        Record();
        Refresh();
    }

    private void CheckSyntax()
    {
        try
        {
            Engine.PrepareScript(CurrentSlide.Script);
            ScriptStatus.Text = "Syntaxe JavaScript valide";
            ScriptStatus.Foreground = Ui.Brush("#347955");
        }
        catch (Exception exception)
        {
            ScriptStatus.Text = exception.Message;
            ScriptStatus.Foreground = Ui.Brush("#AD273C");
        }
    }

    private void UpdateStatus(string? message = null, bool error = false)
    {
        DeckTitle.Text = _deck.Name + (IsDirty ? " *" : "");
        Title = $"Presenter — {_deck.Name}";
        Status.Text = message ?? (IsDirty ? "Modifications à enregistrer" : _path == null ? "Présentation locale" : "Présentation enregistrée");
        StatusDot.Background = error ? Ui.Brush("#AD273C") : IsDirty ? Ui.Brush("#DBA041") : Ui.Brush("#329467");

        UndoButton.IsEnabled = _history.CanUndo;
        RedoButton.IsEnabled = _history.CanRedo;
    }

    private void Record(string? group = null)
    {
        _history.Record(_deck, _slideId, group);
        UpdateStatus();
    }

    private void ChangeHistory(bool redo)
    {
        DeckHistory.Entry? entry = redo ? _history.Redo() : _history.Undo();
        if (entry == null)
            return;

        _deck = DeckFile.ReadSnapshot(entry.Content);
        _slideId = entry.SlideId;
        Refresh();
    }

    private void OpenSlide(Guid id)
    {
        if (id == _slideId)
            return;

        _history.SelectSlide(id);
        _slideId = id;
        _elementId = null;
        Refresh();
    }

    private void Navigate(int offset) =>
        OpenSlide(_deck.Slides[Math.Clamp(SlideIndex + offset, 0, _deck.Slides.Count - 1)].Id);

    private void BuildLayoutMenu(MenuFlyout menu)
    {
        menu.Items.Clear();
        IEnumerable<string> layouts = new[] { "title", "slide", "end" }
            .Concat(CurrentTheme.Layouts.Keys)
            .Distinct(StringComparer.Ordinal)
            .Append("blank");

        foreach (string layout in layouts)
        {
            MenuItem item = new() { Header = CurrentTheme.GetLabel(layout) };
            item.Click += (_, _) => AddSlide(layout);
            menu.Items.Add(item);
        }
    }
}

internal sealed record SlideCard(Guid Id, string Number, string Name, bool Active, SlideSurface Preview);

internal sealed record ElementChip(string Id, string Icon, bool Active);
