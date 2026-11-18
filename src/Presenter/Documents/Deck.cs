namespace Presenter.Documents;

internal sealed class Deck
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Sans titre";
    public string Theme { get; set; } = "signal";
    public List<Slide> Slides { get; set; } = [];
}

internal sealed class Slide
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nouvelle diapo";
    public string Layout { get; set; } = "slide";
    public string Notes { get; set; } = "";
    public string Script { get; set; } = Templates.EmptyScript;
    public List<SlideElement> Elements { get; set; } = [];
}
