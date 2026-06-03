using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.SemanticKernel;

namespace backend.Orchestration
{
    public class ReportGenerator
    {
        private readonly Kernel _kernel;

        public ReportGenerator(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task GenerateFinalReportAsync(AgentState state)
        {
            Console.WriteLine("[Report Generator] Generating final report");

            if (state.GlobalConfidenceScore < 60)
            {
                state.FinalReport = "Insufficient supporting evidence found in the knowledge base.";
                return;
            }

            string agentFindings = string.Join("\n\n", state.Evidence.Where(e => !string.IsNullOrEmpty(e.Findings)).Select(e => $"[{e.SourceAgent.ToUpper()}]\n{e.Findings}"));
            string rawChunks = string.Join("\n\n", state.Evidence.Where(e => !string.IsNullOrEmpty(e.ChunkText)).Select(e => e.ChunkText));

            string prompt = $@"
You are a Report Generation Agent.
Using the following agent findings and raw evidence, generate a comprehensive, structured response to the user's query.
Format the output in clean Markdown. Do NOT include markdown code blocks around the entire response.

If the confidence score is between 60 and 85, you MUST append this exact warning to the very end of the report:
> [!WARNING]
> **Confidence Score: {state.GlobalConfidenceScore:F1}%**. Verification recommended.

Agent Findings:
{agentFindings}

Raw Evidence Context:
{rawChunks}

User Query:
{state.OriginalQuery}";

            var result = await _kernel.InvokePromptAsync(prompt);
            state.FinalReport = result.GetValue<string>()?.Trim() ?? "Error generating report.";
            state.IsComplete = true;
        }
    }
}
