using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Models;
using backend.Services;

namespace backend.Agents
{
    public class ResearchAgent : BaseAgent
    {
        private readonly RetrievalService _retrievalService;
        private readonly GraphService _graphService;

        public override string AgentName => "research";

        public ResearchAgent(RetrievalService retrievalService, GraphService graphService)
        {
            _retrievalService = retrievalService;
            _graphService = graphService;
        }

        public override async Task ExecuteAsync(AgentState state)
        {
            Console.WriteLine($"[Agent: {AgentName}] Executing research for query: {state.OriginalQuery}");

            var (chunks, sources, confidence) = await _retrievalService.GetRelevantChunksAsync(state.RewrittenQuery);

            if (sources != null && sources.Any())
            {
                state.Sources.AddRange(sources);
            }

            foreach (var chunk in chunks)
            {
                state.Evidence.Add(new EvidenceNode
                {
                    SourceAgent = AgentName,
                    ChunkText = chunk,
                    Confidence = confidence
                });
            }

            // Extract keywords for Graph
            var words = state.RewrittenQuery.Split(' ').Where(w => w.Length > 4).ToList();
            string graphContext = await _graphService.GetGraphContextAsync(words);

            if (!string.IsNullOrEmpty(graphContext))
            {
                state.Evidence.Add(new EvidenceNode
                {
                    SourceAgent = AgentName + "_graph",
                    ChunkText = graphContext,
                    Confidence = 100
                });
            }
        }
    }
}
