using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.SemanticKernel;

namespace backend.Agents
{
    public class VerificationAgent : BaseAgent
    {
        private readonly Kernel _kernel;

        public override string AgentName => "verification";

        public VerificationAgent(Kernel kernel)
        {
            _kernel = kernel;
        }

        public override async Task ExecuteAsync(AgentState state)
        {
            Console.WriteLine($"[Agent: {AgentName}] Executing verification");

            if (!state.Evidence.Any())
            {
                state.GlobalConfidenceScore = 0;
                return;
            }

            string agentFindings = string.Join("\n\n", state.Evidence.Where(e => e.SourceAgent != "research").Select(e => e.Findings));
            if (string.IsNullOrWhiteSpace(agentFindings))
            {
                // Just relying on research chunks
                state.GlobalConfidenceScore = state.Evidence.Where(e => e.SourceAgent == "research").Average(e => e.Confidence);
                return;
            }

            string prompt = $@"
You are a Verification Agent.
Review the following agent findings against the user query.
Determine the confidence score (0 to 100) that the findings accurately and factually answer the query based ONLY on the evidence.
Return EXACTLY a number between 0 and 100. Do not include any other text.

Agent Findings:
{agentFindings}

User Query:
{state.OriginalQuery}";

            var result = await _kernel.InvokePromptAsync(prompt);
            string scoreStr = result.GetValue<string>()?.Trim() ?? "50";
            
            if (double.TryParse(scoreStr, out double parsedScore))
            {
                state.GlobalConfidenceScore = parsedScore;
            }
            else
            {
                state.GlobalConfidenceScore = 50;
            }
        }
    }
}
