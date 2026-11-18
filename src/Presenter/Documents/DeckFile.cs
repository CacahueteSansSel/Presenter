using System.Text.Json;
using System.Text.Json.Serialization;

namespace Presenter.Documents;

internal static class DeckFile
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Snapshot(Deck deck) => JsonSerializer.Serialize(deck, Json);

    public static Deck ReadSnapshot(string snapshot) =>
        JsonSerializer.Deserialize<Deck>(snapshot, Json) ?? throw new InvalidDataException("Présentation vide.");

    public static T Copy<T>(T value) where T : class =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json) ??
        throw new InvalidDataException("Élément vide.");

    public static async Task<DeckPackage> LoadAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);

        DeckPackage? package = await JsonSerializer.DeserializeAsync<DeckPackage>(stream, Json);
        if (package?.Deck == null || package.Deck.Version != 1 ||
            package.Deck.Slides == null || package.Deck.Slides.Count == 0 || package.Images == null || package.Fonts == null)
        {
            throw new InvalidDataException("Format Presenter inconnu ou présentation vide.");
        }

        Deck deck = package.Deck;
        if (deck.Name == null || deck.Theme == null ||
            deck.Slides.Any(slide => slide == null || slide.Name == null || slide.Layout == null ||
                slide.Script == null || slide.Notes == null || slide.Elements == null))
        {
            throw new InvalidDataException("Une diapo est incomplète.");
        }

        foreach (Slide slide in deck.Slides)
        {
            HashSet<string> ids = new(StringComparer.Ordinal);
            if (slide.Elements.Any(element => element == null || element.Id == null ||
                !Templates.IsValidId(element.Id) || !ids.Add(element.Id) || element.Content == null ||
                element.Font == null || element.Color == null || element.Caption == null ||
                !double.IsFinite(element.X) || !double.IsFinite(element.Y) ||
                !double.IsFinite(element.Angle) || element.Width < 1 || element.Height < 1 ||
                element.FontSize < 1 || !Enum.IsDefined(element.Kind) ||
                !double.IsFinite(element.Width) || !double.IsFinite(element.Height) ||
                !double.IsFinite(element.FontSize)))
            {
                throw new InvalidDataException($"Un élément de « {slide.Name} » est invalide.");
            }
        }

        return package;
    }

    public static async Task SaveAsync(
        string path, Deck deck, Dictionary<string, byte[]> images, Dictionary<string, byte[]>? fonts = null)
    {
        SlideElement[] elements = deck.Slides.SelectMany(slide => slide.Elements).ToArray();
        HashSet<string> usedImages = elements.Select(element => element.Asset)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> usedFonts = elements.Select(element => element.Font).ToHashSet(StringComparer.Ordinal);
        fonts ??= [];

        DeckPackage package = new()
        {
            Deck = deck,
            Images = images.Where(entry => usedImages.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
            Fonts = fonts.Where(entry => usedFonts.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
        };

        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, package, Json);

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}

internal sealed class DeckPackage
{
    public Deck? Deck { get; set; }
    public Dictionary<string, byte[]> Images { get; set; } = [];
    public Dictionary<string, byte[]> Fonts { get; set; } = [];
}
