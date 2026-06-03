using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.SemanticKernel;

namespace backend.Agents
{
    public class RiskAnalysisAgent : BaseAgent
    {
        private readonly Kernel _kernel;

        public override string AgentName => "risk_analysis";

        public RiskAnalysisAgent(Kernel kernel)
        {
            _kernel = kernel;
        }

        public override async Task ExecuteAsync(AgentState state)
        {
            Console.WriteLine($"[Agent: {AgentName}] Executing risk analysis");

            string contextStr = string.Join("\n\n", state.Evidence.Select(e => e.ChunkText));
            if (string.IsNullOrWhiteSpace(contextStr)) return;

            string prompt = $@"
You are a Risk Analysis Agent.
Review the following documents or clauses.
Identify:
- Missing information
- Ambiguities
- Compliance risks
- Operational risks

Context:
{contextStr}

User Query:
{state.OriginalQuery}

Return your risk analysis clearly structured.";

            var result = await _kernel.InvokePromptAsync(prompt);
            string findings = result.GetValue<string>()?.Trim() ?? string.Empty;

            state.Evidence.Add(new EvidenceNode
            {
                SourceAgent = AgentName,
                Findings = findings,
                Confidence = 85
            });
        }
    }
}
