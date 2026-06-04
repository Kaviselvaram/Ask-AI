using System.Collections.Generic;

namespace backend.Models
{
    public class AgentState
    {
        public string OriginalQuery { get; set; } = string.Empty;
        public string RewrittenQuery { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public List<string> ExecutionPlan { get; set; } = new();
        public List<EvidenceNode> Evidence { get; set; } = new();
        public List<SourceInfo> Sources { get; set; } = new();
        public double GlobalConfidenceScore { get; set; } = 0.0;
        public string FinalReport { get; set; } = string.Empty;
        public bool IsComplete { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ConversationHistory { get; set; } = string.Empty;
        public string RecentEntities { get; set; } = string.Empty;
    }

    public class EvidenceNode
    {
        public string SourceAgent { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string ChunkText { get; set; } = string.Empty;
        public string Findings { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}
