namespace SDHome.Lib.Models;

public record Zone
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public int? ParentZoneId { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    
    // For hierarchical display
    public Zone? ParentZone { get; init; }
    public List<Zone> ChildZones { get; init; } = [];
    
    /// <summary>
    /// Gets the full path of the zone hierarchy
    /// e.g., "SD Home / Downstairs / Front Room"
    /// </summary>
    public string FullPath => BuildFullPath();
    
    /// <summary>
    /// Gets the depth level (0 = root, 1 = first child level, etc.)
    /// </summary>
    public int Depth => ParentZone?.Depth + 1 ?? 0;

    private string BuildFullPath()
    {
        if (ParentZone == null)
            return Name;
        
        return $"{ParentZone.FullPath} / {Name}";
    }
}

public record CreateZoneRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public int? ParentZoneId { get; init; }
    public int SortOrder { get; init; }
}

public record UpdateZoneRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public int? ParentZoneId { get; init; }
    public int SortOrder { get; init; }
}
