using System.Collections.Generic;

namespace backend.Models
{
    public record ExecutionPlan(
        string Goal,
        string Strategy,
        List<string> Steps,
        int RecommendedChunkCount,
        bool RequiresComparison,
        bool RequiresMultiDocumentReasoning,
        bool RequiresKnowledgeGraph
    );
}
