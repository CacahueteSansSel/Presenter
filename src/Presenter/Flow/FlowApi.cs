namespace Presenter.Flow;

internal static class FlowApi
{
    public static readonly FlowMethod[] Methods =
    [
        new("beforePresent", "beforePresent()", "Exécutée à l’entrée de la diapo."),
        new("part", "part(async () => { … })", "Ajoute une étape déclenchée au clic suivant."),
        new("afterPresent", "afterPresent()", "Exécutée avant de quitter la diapo."),
        new("fadeIn", "fadeIn(id, ms = 400)", "Fait apparaître un élément. Utiliser await pour attendre le fondu."),
        new("fadeOut", "fadeOut(id, ms = 400)", "Fait disparaître un élément. Utiliser await pour attendre le fondu."),
        new("show", "show(id)", "Affiche immédiatement un élément."),
        new("hide", "hide(id)", "Masque immédiatement un élément."),
        new("wait", "wait(ms)", "Attend en millisecondes. Utiliser await."),
        new("text", "text(id, value)", "Remplace le texte ou le code d’un élément. Les textes acceptent le Markdown."),
        new("move", "move(id, x, y)", "Positionne un élément sur la grille 1200 × 675."),
        new("rotate", "rotate(id, degrees)", "Applique une rotation en degrés.")
    ];
}

internal sealed record FlowMethod(string Name, string Signature, string Description);
