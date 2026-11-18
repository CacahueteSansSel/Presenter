using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Presenter.Documents;

namespace Presenter.Views;

internal sealed partial class MainWindow
{
    private static readonly FilePickerFileType PresenterFile = new("Présentation Presenter")
    {
        Patterns = ["*.presenter"]
    };

    private void OnShortcut(object? sender, KeyEventArgs args)
    {
        bool command = (args.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        bool shift = args.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (command)
        {
            switch (args.Key)
            {
                case Key.S:
                    _ = RunFileAsync(() => SaveAsync(saveAs: shift));
                    break;
                case Key.O:
                    _ = RunFileAsync(ChooseOpenAsync);
                    break;
                case Key.N:
                    _ = RunFileAsync(NewAsync);
                    break;
                case Key.Z:
                    ChangeHistory(shift);
                    break;
                case Key.Y:
                    ChangeHistory(true);
                    break;
                case Key.D:
                    if (CurrentElement != null)
                        DuplicateElement();
                    else
                        DuplicateSlide();
                    break;
                default:
                    return;
            }

            args.Handled = true;

            return;
        }

        if (args.Key == Key.F5)
        {
            Present(fromBeginning: !shift);
            args.Handled = true;

            return;
        }

        if (args.Source is Visual source && IsEditingText(source))
            return;

        switch (args.Key)
        {
            case Key.Delete:
            case Key.Back when CurrentElement != null:
                DeleteElement();
                break;
            case Key.PageUp:
                Navigate(-1);
                break;
            case Key.PageDown:
                Navigate(1);
                break;
            case Key.Escape:
                SelectElement(null);
                break;
            default:
                return;
        }

        args.Handled = true;
    }

    private static bool IsEditingText(Visual source) =>
        source is TextBox || source is TextEditor ||
        source.GetVisualAncestors().Any(ancestor => ancestor is TextEditor || ancestor is TextBox);

    private async Task RunFileAsync(Func<Task> action)
    {
        if (_fileBusy)
            return;

        _fileBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            UpdateStatus(exception.Message, error: true);
            await ShowMessageAsync("Opération impossible", exception.Message);
        }
        finally
        {
            _fileBusy = false;
        }
    }

    private async Task NewAsync()
    {
        if (!await ConfirmChangesAsync())
            return;

        Deck deck = new() { Theme = CurrentTheme.Id };
        deck.Slides.Add(Templates.CreateSlide("title", CurrentTheme));
        InstallDeck(deck, [], [], null);
        ShowTab(code: false);
    }

    private async Task ChooseOpenAsync()
    {
        if (!await ConfirmChangesAsync())
            return;

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ouvrir une présentation",
            FileTypeFilter = [PresenterFile]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is string path)
            await ReadFileAsync(path);
    }

    public Task OpenPathAsync(string path) => RunFileAsync(async () =>
    {
        if (await ConfirmChangesAsync())
            await ReadFileAsync(path);
    });

    private async Task ReadFileAsync(string path)
    {
        DeckPackage package = await DeckFile.LoadAsync(path);
        if (package.Deck != null)
            InstallDeck(package.Deck, package.Images, package.Fonts, path);
    }

    private void InstallDeck(
        Deck deck, Dictionary<string, byte[]> images, Dictionary<string, byte[]> fonts, string? path)
    {
        SlideImages previousImages = _images;
        _images = new SlideImages(images);
        _fonts = fonts;
        foreach ((string reference, byte[] bytes) in fonts)
        {
            try
            {
                FontCatalog.Import(bytes, reference);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceWarning($"Police intégrée : {exception.Message}");
            }
        }

        _path = path;
        _deck = deck;
        _slideId = deck.Slides[0].Id;
        _elementId = null;
        _history = new DeckHistory(deck, _slideId);
        _savedSnapshot = _history.Current;

        Refresh();

        previousImages.Dispose();
    }

    private async Task<bool> SaveAsync(bool saveAs = false)
    {
        string? path = _path;
        if (saveAs || path == null)
        {
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Enregistrer la présentation",
                SuggestedFileName = _deck.Name,
                DefaultExtension = "presenter",
                FileTypeChoices = [PresenterFile]
            });
            path = file?.TryGetLocalPath();
        }

        if (path == null)
            return false;

        _history.BreakGroup();
        string snapshot = _history.Current;
        await DeckFile.SaveAsync(path, DeckFile.ReadSnapshot(snapshot), _images.Data, _fonts);
        _path = path;
        _savedSnapshot = snapshot;
        UpdateStatus();

        return true;
    }

    private async Task<bool> ConfirmChangesAsync()
    {
        if (!IsDirty)
            return true;

        string? choice = await MessageDialogAsync(
            "Enregistrer les modifications ?",
            $"Les modifications de « {_deck.Name} » sont en attente.",
            ["Annuler", "Ignorer", "Enregistrer"]);

        return choice switch
        {
            "Ignorer" => true,
            "Enregistrer" => await SaveAsync(),
            _ => false
        };
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_allowClose || !IsDirty)
            return;

        args.Cancel = true;
        if (_confirmingClose || _fileBusy)
            return;

        _confirmingClose = true;
        try
        {
            if (await ConfirmChangesAsync())
            {
                _allowClose = true;
                Close();
            }
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("Enregistrement impossible", exception.Message);
        }
        finally
        {
            _confirmingClose = false;
        }
    }

    private async Task OpenThemeFolderAsync()
    {
        await RunFileAsync(async () =>
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string path = Path.Combine(root, "Presenter", "themes");
            Directory.CreateDirectory(path);
            IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(path);
            if (folder != null)
                await Launcher.LaunchFileAsync(folder);
        });
    }

    private Task ShowMessageAsync(string title, string message) => MessageDialogAsync(title, message, ["Fermer"]);

    private Task<string?> MessageDialogAsync(string title, string message, string[] labels)
    {
        MessageWindow dialog = new(title, message, labels.Length > 1);

        return dialog.ShowDialog<string?>(this);
    }

    private void Present(bool fromBeginning)
    {
        if (_presentation != null)
        {
            _presentation.Activate();

            return;
        }

        CodeEditor.DismissCompletion();
        Deck copy = DeckFile.ReadSnapshot(DeckFile.Snapshot(_deck));
        PresentationWindow presentation = new(copy, fromBeginning ? 0 : SlideIndex, CurrentTheme, new SlideImages(_images.Data));
        presentation.Position = Position;
        presentation.Closed += (_, _) => _presentation = null;
        _presentation = presentation;
        presentation.Show();
    }

    private void AddSlide(string layout)
    {
        Slide slide = Templates.CreateSlide(layout, CurrentTheme);
        _deck.Slides.Insert(SlideIndex + 1, slide);
        _slideId = slide.Id;
        _elementId = null;
        Record();
        Refresh();
    }

    private void DuplicateSlide()
    {
        Slide copy = DeckFile.Copy(CurrentSlide);
        copy.Id = Guid.NewGuid();
        copy.Name += " (copie)";

        _deck.Slides.Insert(SlideIndex + 1, copy);
        _slideId = copy.Id;
        _elementId = null;
        Record();
        Refresh();
    }

    private void MoveSlide(int offset)
    {
        int source = SlideIndex;
        int target = source + offset;
        if (target < 0 || target >= _deck.Slides.Count)
            return;

        Slide slide = CurrentSlide;
        _deck.Slides.RemoveAt(source);
        _deck.Slides.Insert(target, slide);
        Record();
        Refresh();
    }

    private void DeleteSlide()
    {
        int index = SlideIndex;
        _deck.Slides.RemoveAt(index);

        if (_deck.Slides.Count == 0)
            _deck.Slides.Add(Templates.CreateSlide("blank", CurrentTheme));

        _slideId = _deck.Slides[Math.Min(index, _deck.Slides.Count - 1)].Id;
        _elementId = null;
        Record();
        Refresh();
    }

    private void AddElement(ElementKind kind, string? content = null, double size = 33, bool bold = false)
    {
        string prefix = kind switch
        {
            ElementKind.Text => "text",
            ElementKind.Code => "code",
            ElementKind.Window => "window",
            _ => "image"
        };
        int offset = (CurrentSlide.Elements.Count % 7) * 14;

        SlideElement element = new()
        {
            Id = Templates.UniqueId(CurrentSlide, prefix),
            X = 125 + offset,
            Y = 120 + offset,
            Kind = kind,
            Content = content ?? "const greeting = \"Bonjour\";\nconsole.log(greeting);",
            Caption = kind == ElementKind.Window ? "exemple.js" : "",
            FontSize = kind == ElementKind.Text ? size : 24,
            Font = kind == ElementKind.Text ? "Inter" : "JetBrains Mono",
            Width = kind == ElementKind.Text ? 520 : 545,
            Height = kind == ElementKind.Text ? Math.Max(145, size * 2) : 216,
            Bold = bold
        };

        InsertElement(element);
    }

    private void InsertElement(SlideElement element)
    {
        CurrentSlide.Elements.Add(element);
        _elementId = element.Id;
        Record();
        Refresh();
        ShowTab(code: false);
    }

    private void DuplicateElement()
    {
        if (CurrentElement is not SlideElement selected)
            return;

        SlideElement copy = DeckFile.Copy(selected);
        copy.Id = Templates.UniqueId(CurrentSlide, selected.Id);
        copy.X += 25;
        copy.Y += 25;
        InsertElement(copy);
    }

    private void DeleteElement()
    {
        if (CurrentElement is not SlideElement selected)
            return;

        CurrentSlide.Elements.Remove(selected);
        _elementId = null;
        Record();
        Refresh();
    }

    private void ReorderElement(bool front)
    {
        if (CurrentElement is not SlideElement selected)
            return;

        List<SlideElement> elements = CurrentSlide.Elements;
        elements.Remove(selected);
        elements.Insert(front ? elements.Count : 0, selected);
        Record();
        Refresh();
    }

    private void RunImageImport(bool replace = false) => _ = RunFileAsync(() => ImportImageAsync(replace));

    private async Task ImportImageAsync(bool replace)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Insérer une image",
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.bmp"] }]
        });

        if (files.FirstOrDefault() is not IStorageFile selected)
            return;

        await using Stream source = await selected.OpenReadAsync();
        using MemoryStream buffer = new();
        await source.CopyToAsync(buffer);
        buffer.Position = 0;

        Bitmap bitmap = new(buffer);
        string asset = Guid.NewGuid().ToString("N");
        _images.Add(asset, buffer.ToArray(), bitmap);

        if (replace && CurrentElement is SlideElement element && element.Kind == ElementKind.Image)
        {
            element.Asset = asset;
            element.Caption = selected.Name;
            Record();
            Refresh();

            return;
        }

        double scale = Math.Min(565d / bitmap.PixelSize.Width, 395d / bitmap.PixelSize.Height);
        InsertElement(new SlideElement
        {
            Id = Templates.UniqueId(CurrentSlide, "image"),
            Kind = ElementKind.Image,
            Caption = selected.Name,
            Asset = asset,
            X = 140,
            Y = 120,
            Width = Math.Max(1, bitmap.PixelSize.Width * scale),
            Height = Math.Max(1, bitmap.PixelSize.Height * scale)
        });
    }

    private async Task ImportFontAsync(SlideElement element)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer une police",
            FileTypeFilter = [new FilePickerFileType("Polices TrueType et OpenType") { Patterns = ["*.ttf", "*.otf"] }]
        });
        if (files.FirstOrDefault() is not IStorageFile selected)
            return;

        await using Stream source = await selected.OpenReadAsync();
        if (source.Length > 12 * 1024 * 1024)
            throw new InvalidDataException("La police dépasse 12 Mio.");

        using MemoryStream buffer = new();
        await source.CopyToAsync(buffer);
        byte[] bytes = buffer.ToArray();

        FontChoice font = FontCatalog.Import(bytes);
        if (CurrentElement != element)
            return;

        _fonts[font.Reference] = bytes;
        EditElement(selected => selected.Font = font.Reference);
        BuildInspector();
    }
}
