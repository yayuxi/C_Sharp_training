namespace ScraperTemplate.Helpers;

/// <summary>
/// A lightweight representation of a page element sent to the AI.
/// Much cheaper than sending full HTML — strips everything irrelevant.
/// </summary>
public class ElementSummary
{
    public int Index { get; set; }
    public string Text { get; set; } = "";
    public string Href { get; set; } = "";
    public string CssClass { get; set; } = "";
    public string TagName { get; set; } = "";
    public string ParentText { get; set; } = "";
    public string ParentClass { get; set; } = "";
    public string Selector { get; set; } = "";

    public override string ToString() =>
        $"{Index}. \"{Text}\" | tag={TagName} | class=\"{CssClass}\" " +
        $"| href=\"{Href}\" | parent=\"{ParentClass}\"";
}