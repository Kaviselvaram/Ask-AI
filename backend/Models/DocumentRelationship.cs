namespace backend.Models;

public class DocumentRelationship
{
    public string SourceFileName { get; set; } = string.Empty;
    public string TargetFileName { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string SharedContext { get; set; } = string.Empty;
}
