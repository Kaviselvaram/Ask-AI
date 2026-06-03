using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.SemanticKernel;

namespace backend.Agents
{
    public class ComparisonAgent : BaseAgent
    {
        private readonly Kernel _kernel;

        public override string AgentName => "comparison";

        public ComparisonAgent(Kernel kernel)
        {
            _kernel = kernel;
        }

        public override async Task ExecuteAsync(AgentState state)
        {
            Console.WriteLine($"[Agent: {AgentName}] Executing comparison");

            string contextStr = string.Join("\n\n", state.Evidence.Select(e => e.ChunkText));
            if (string.IsNullOrWhiteSpace(contextStr)) return;

            string prompt = $@"
You are a Comparison Analyst Agent.
Compare the following documents or clauses based on the user's query.
Identify explicitly:
- Agreements
- Conflicts/Contradictions
- Additions
- Removals

Context:
{contextStr}

User Query:
{state.OriginalQuery}

Return your comparison findings clearly structured.";

            var result = await _kernel.InvokePromptAsync(prompt);
            string findings = result.GetValue<string>()?.Trim() ?? string.Empty;

            state.Evidence.Add(new EvidenceNode
            {
                SourceAgent = AgentName,
                Findings = findings,
                Confidence = 90
            });
        }
    }
}
