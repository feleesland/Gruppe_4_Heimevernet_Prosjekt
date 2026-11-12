namespace Heimevernet_Prosjekt.Models.Entities;

// What kind of answer a question has
public enum AttributeKind
{
    YesNo,   // ☑ e.g. "Hengerfeste"
    Number,  // 🔢 e.g. "Antall seter", "Effekt (kW)"
    Choice,  // ◉ one of the Options, e.g. "B;BE;C"
    Text     // ✎ free text
}

// One question for a resource type, e.g. "Hengerfeste" for Bil. Admin decides the questions, the owner answers them (help with AI)
public class ResourceAttribute
{
    public int Id { get; set; }
    public int ResourceTypeId { get; set; }
    public ResourceType ResourceType { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public AttributeKind Kind { get; set; }

    // Choice: the options separated by ";" (e.g. "B;BE;C")
    public string? Options { get; set; }

    // Number: the unit shown after the number (e.g. "kW", "tonn")
    public string? Unit { get; set; }

    public int SortOrder { get; set; }

    // Hidden instead of deleted, so answers that are already given are kept
    public bool IsHidden { get; set; }
}

// One answer: "this car has a tow bar". Saved as text, e.g. "true", "5" or "BE"
public class ResourceAttributeValue
{
    public int Id { get; set; }
    public int ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public int ResourceAttributeId { get; set; }
    public ResourceAttribute ResourceAttribute { get; set; } = null!;
    public string Value { get; set; } = string.Empty;
}