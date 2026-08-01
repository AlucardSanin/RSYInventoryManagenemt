namespace RSYInventory.Web.Components.Shared;

public sealed record ComboOption(string Value, string Label, string? SearchText = null)
{
    public string Haystack => SearchText ?? Label;
}
