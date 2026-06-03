using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.SemanticKernel;

namespace backend.Agents
{
    public class ExecutiveSummaryAgent : BaseAgent
    {
        private readonly Kernel _kernel;

        public override string AgentName => "executive_summary";

        public ExecutiveSummaryAgent(Kernel kernel)
        {
            _kernel = kernel;
        }

        public override async Task ExecuteAsync(AgentState state)
        {
            Console.WriteLine($"[Agent: {AgentName}] Executing executive summary");

            string contextStr = string.Join("\n\n", state.Evidence.Select(e => e.ChunkText));
            if (string.IsNullOrWhiteSpace(contextStr)) return;

            string prompt = $@"
You are an Executive Reporting Agent.
Generate a high-level summary of the documents and the situation.
Output should include:
- Key Findings
- Action Items
- Recommendations

Context:
{contextStr}

User Query:
{state.OriginalQuery}

Return your executive summary clearly structured.";

            var result = await _kernel.InvokePromptAsync(prompt);
            string findings = result.GetValue<string>()?.Trim() ?? string.Empty;

            state.Evidence.Add(new EvidenceNode
            {
                SourceAgent = AgentName,
                Findings = findings,
                Confidence = 95
            });
        }
    }
}
