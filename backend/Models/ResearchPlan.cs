using System.Collections.Generic;

namespace backend.Models;

public class ResearchPlan
{
    public string Objective { get; set; } = string.Empty;
    public List<string> InvestigationSteps { get; set; } = new();
    public List<string> EvidenceSources { get; set; } = new();
    public List<string> Findings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}
