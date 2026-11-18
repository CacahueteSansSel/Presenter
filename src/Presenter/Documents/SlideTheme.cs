using System.Text.Json;
using Avalonia.Media.Imaging;

namespace Presenter.Documents;

internal sealed class SlideTheme
{
    private readonly Dictionary<string, Bitmap?> _backgrounds = new(StringComparer.Ordinal);

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Animate { get; set; }
    public Dictionary<string, ThemeLayout> Layouts { get; set; } = new(StringComparer.Ordinal);
    public string DirectoryPath { get; set; } = "";

    public string GetLabel(string layout)
    {
        if (Layouts.TryGetValue(layout, out ThemeLayout? template) && !string.IsNullOrWhiteSpace(template.Name))
            return template.Name;

        return layout switch
        {
            "title" => "Titre",
            "slide" => "Contenu",
            "end" => "Conclusion",
            "blank" => "Vierge",
            _ => layout
        };
    }

    public Bitmap? GetBackground(string layout)
    {
        if (layout == "blank")
            return null;

        if (_backgrounds.TryGetValue(layout, out Bitmap? background))
            return background;

        string key = Layouts.ContainsKey(layout) ? layout : "slide";
        string path = Path.Combine(DirectoryPath, key + ".png");

        try
        {
            background = File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceWarning($"Thème {Id} : {exception.Message}");
        }

        _backgrounds[layout] = background;

        return background;
    }

    public static IReadOnlyList<SlideTheme> Discover()
    {
        Dictionary<string, SlideTheme> themes = new(StringComparer.Ordinal);
        string userDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Presenter", "themes");

        foreach (string root in new[] { Path.Combine(AppContext.BaseDirectory, "themes"), userDirectory })
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string path in Directory.EnumerateDirectories(root))
            {
                try
                {
                    SlideTheme? theme = Read(path);
                    if (theme != null)
                        themes[theme.Id] = theme;
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Trace.TraceWarning($"Thème {path} : {exception.Message}");
                }
            }
        }

        return themes.Values.OrderBy(theme => theme.Id != "signal")
            .ThenBy(theme => theme.Name)
            .ToList();
    }

    private static SlideTheme? Read(string path)
    {
        string id = Path.GetFileName(path);
        string manifest = Path.Combine(path, "theme.json");
        SlideTheme theme = File.Exists(manifest)
            ? JsonSerializer.Deserialize<SlideTheme>(File.ReadAllText(manifest), DeckFile.Json) ?? new SlideTheme()
            : new SlideTheme();

        theme.Id = id;
        theme.DirectoryPath = path;
        theme.Name = string.IsNullOrWhiteSpace(theme.Name) ? char.ToUpperInvariant(id[0]) + id[1..] : theme.Name;

        Dictionary<string, ThemeLayout> layouts = new(StringComparer.Ordinal);
        foreach (string filename in Directory.EnumerateFiles(path, "*.png").Order(StringComparer.Ordinal))
        {
            string key = Path.GetFileNameWithoutExtension(filename);
            if (key != "blank")
                layouts.Add(key, theme.Layouts.GetValueOrDefault(key) ?? new ThemeLayout());
        }

        theme.Layouts = layouts;

        return layouts.Count > 0 ? theme : null;
    }
}

internal sealed class ThemeLayout
{
    public string? Name { get; set; }
    public List<SlideElement>? Elements { get; set; }
    public List<ThemeMask> Masks { get; set; } = [];
}

internal sealed class ThemeMask
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Color { get; set; } = "#FFFFFF";
}
