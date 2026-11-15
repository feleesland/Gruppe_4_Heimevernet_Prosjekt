using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Heimevernet_Prosjekt.Services;

namespace Heimevernet_Prosjekt.Models.ViewModels;

// The area picker (Views/Shared/_RegionPicker.cshtml), used on Oversikt, Ressurser and Behov (help with AI)
public class RegionPickerViewModel
{
    // The chosen kommune, "alle" (Hele Norge), or null = not chosen
    public string? Region { get; set; }
    public bool AllNorway => Region == RegionList.All;
    public string RegionName => Region == null || AllNorway ? "Hele Norge" : Region;

    // The kommuner to choose from, the most active first
    public List<OverviewRegion> Regions { get; set; } = new();

    // The page that is opened again with ?region=…
    public string Url { get; set; } = "";

    // After the name on Oversikt, e.g. "3 aktive behov · 12 ressurser" (the lists show the number in their own bar)
    public string Summary { get; set; } = "";

    // true: at the start of the list bar, with the picker as a box under it (the lists). false: the big line (Oversikt)
    public bool Compact { get; set; }

    // true: the picker is open, and must be used first (Oversikt before an area is chosen)
    public bool OpenPicker { get; set; }
}
