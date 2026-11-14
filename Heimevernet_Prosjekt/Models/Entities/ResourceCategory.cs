namespace Heimevernet_Prosjekt.Models.Entities;

// A group of resource types in "Ny ressurs", e.g. "Kjøretøy" with Bil, Traktor and Båt (help with AI)
public class ResourceCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Icon name from Tabler Icons, e.g. "car" - Docs: https://tabler.io/icons
    public string Icon { get; set; } = string.Empty;

    // The order on the page (lowest first)
    public int SortOrder { get; set; }

    public List<ResourceType> Types { get; set; } = new();
}