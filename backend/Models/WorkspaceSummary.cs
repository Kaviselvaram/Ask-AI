using System.Collections.Generic;

namespace backend.Models;

public class WorkspaceSummary
{
    public int TotalDocuments { get; set; }
    public List<string> Categories { get; set; } = new();
    public string OverallSummary { get; set; } = string.Empty;
    public List<DocumentProfile> Profiles { get; set; } = new();
    public List<DocumentRelationship> Relationships { get; set; } = new();
}
