using System.Security.Cryptography;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Presenter.Views;

internal static class FontCatalog
{
    private static readonly Dictionary<string, FontChoice> Imported = new(StringComparer.Ordinal);

    public static FontChoice[] Choices(IEnumerable<string> references) =>
        new[] { "Inter", "JetBrains Mono", "Arial" }
            .Concat(FontManager.Current.SystemFonts.Select(font => font.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new FontChoice(name, name))
            .Concat(references.Where(Imported.ContainsKey).Select(reference => Imported[reference]))
            .OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public static FontFamily Resolve(string family) => family switch
    {
        "Inter" => Ui.Inter,
        "JetBrains Mono" => Ui.Mono,
        _ => new FontFamily(family)
    };

    public static FontChoice Import(byte[] bytes, string? expectedReference = null)
    {
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string key = $"fonts:presenter-{hash}";
        FontChoice? existing = Imported.Values.FirstOrDefault(choice => choice.Reference.StartsWith(key + "#", StringComparison.Ordinal));
        if (existing != null)
        {
            CheckReference(existing.Reference, expectedReference);

            return existing;
        }

        ImportedFont collection = new(new Uri(key));
        using MemoryStream stream = new(bytes, writable: false);
        string? family = collection.Load(stream);
        if (string.IsNullOrWhiteSpace(family))
            throw new InvalidDataException("Impossible de lire cette police TTF ou OTF.");

        string reference = $"{key}#{family}";
        CheckReference(reference, expectedReference);

        FontManager.Current.AddFontCollection(collection);
        FontChoice choice = new(reference, family + " (importée)");
        Imported.Add(reference, choice);

        return choice;
    }

    private static void CheckReference(string reference, string? expected)
    {
        if (expected != null && !string.Equals(reference, expected, StringComparison.Ordinal))
            throw new InvalidDataException("La police intégrée ne correspond pas à la référence de la présentation.");
    }

    private sealed class ImportedFont(Uri key) : FontCollectionBase
    {
        public override Uri Key => key;

        public string? Load(Stream stream) =>
            TryAddGlyphTypeface(stream, out GlyphTypeface? typeface) ? typeface?.FamilyName : null;
    }
}

internal sealed record FontChoice(string Reference, string Name)
{
    public override string ToString() => Name;
}
