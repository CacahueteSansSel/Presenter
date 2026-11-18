using System.Text.RegularExpressions;

namespace Presenter.Documents;

internal static partial class Templates
{
    public const string EmptyScript = """
        async function beforePresent() {
        }

        async function afterPresent() {
        }
        """;

    public static Deck CreateDemo()
    {
        Slide opening = new()
        {
            Layout = "title",
            Name = "Du code à l’écran",
            Notes = "Introduire Presenter. Avancer d’un cran pour faire apparaître le bloc de code.",
            Script = """
                async function beforePresent() {
                    hide("demo");
                    await fadeIn("title", 350);
                }

                part(async () => {
                    await fadeIn("demo", 500);
                });

                async function afterPresent() {
                }
                """,
            Elements =
            [
                Text("eyebrow", "PRÉSENTATIONS TECHNIQUES", 62, 71, 600, 27, 17, true, "#1B6749", "JetBrains Mono"),
                Text("title", "Du code  \nà l’écran.", 58, 143, 570, 242, 101, true),
                Text("subtitle", "Des diapos qui suivent votre rythme.", 64, 424, 540, 65, 28, false, "#35584A"),
                Text("footer", "PRESENTER  /  INTRODUCTION", 64, 617, 720, 35, 15, true, "#2D674F", "JetBrains Mono"),
                new SlideElement
                {
                    Id = "demo",
                    Kind = ElementKind.Window,
                    X = 665,
                    Y = 167,
                    Width = 467,
                    Height = 309,
                    Caption = "demo.js",
                    Content = """
                        // Un clic, une étape
                        part(async () => {
                            await fadeIn("demo", 500);
                        });
                        """,
                    Font = "JetBrains Mono",
                    FontSize = 21
                }
            ]
        };

        Slide content = new()
        {
            Name = "Le déroulé",
            Notes = "La liste se révèle au premier clic.",
            Script = """
                async function beforePresent() {
                    hide("steps");
                }

                part(async () => {
                    await fadeIn("steps", 400);
                });

                async function afterPresent() {
                    await fadeOut("steps", 220);
                }
                """,
            Elements =
            [
                Text("eyebrow", "CODE FLOW", 64, 70, 500, 34, 17, true, "#1B6749", "JetBrains Mono"),
                Text("title", "Le déroulé", 59, 128, 1020, 124, 88, true),
                Text("steps", "1. Préparer les éléments avec **beforePresent**.\n2. Déclencher une étape avec **part**.\n3. Terminer la diapo avec **afterPresent**.",
                    64, 307, 1000, 242, 36)
            ]
        };

        Slide end = new()
        {
            Layout = "end",
            Name = "Questions ?",
            Elements =
            [
                Text("title", "Questions ?", 64, 228, 1072, 143, 105, true, align: TextAlign.Center),
                Text("subtitle", "Merci de votre attention.", 64, 387, 1072, 68, 33, align: TextAlign.Center)
            ]
        };

        return new Deck { Name = "Du code à l’écran", Slides = [opening, content, end] };
    }

    public static Slide CreateSlide(string layout, SlideTheme theme)
    {
        string name = layout switch
        {
            "title" => "Titre",
            "slide" => "Contenu",
            "end" => "Conclusion",
            "blank" => "Vierge",
            _ => theme.GetLabel(layout)
        };

        Slide slide = new() { Name = name, Layout = layout };
        if (layout == "blank")
            return slide;

        if (theme.Layouts.TryGetValue(layout, out ThemeLayout? template) && template.Elements != null)
        {
            slide.Elements = template.Elements.Select(DeckFile.Copy).ToList();

            return slide;
        }

        if (layout == "slide")
        {
            slide.Elements =
            [
                Text("title", "Titre", 62, 78, 1030, 96, 65, true),
                Text("subtitle", "Sous-titre", 64, 184, 1010, 66, 34),
                Text("body", "- Premier point\n- Deuxième point", 64, 294, 830, 250, 34)
            ];

            return slide;
        }

        slide.Elements =
        [
            Text("title", name, 64, 213, 1072, 140, 88, true, align: TextAlign.Center),
            Text("subtitle", "Sous-titre", 64, 365, 1072, 92, 34, align: TextAlign.Center)
        ];

        return slide;
    }

    public static string UniqueId(Slide slide, string prefix)
    {
        int index = 1;
        string id = prefix;

        while (slide.Elements.Any(element => element.Id == id))
            id = prefix + ++index;

        return id;
    }

    public static bool IsValidId(string id) => Identifier().IsMatch(id);

    private static SlideElement Text(
        string id, string text, double x, double y, double width, double height, double size,
        bool bold = false, string color = "#14271E", string font = "Inter", TextAlign align = TextAlign.Left)
    {
        return new SlideElement
        {
            Id = id,
            Content = text,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            FontSize = size,
            Bold = bold,
            Color = color,
            Font = font,
            Align = align
        };
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]{0,63}$")]
    private static partial Regex Identifier();
}
