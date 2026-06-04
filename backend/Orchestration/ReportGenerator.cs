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

            if (state.Evidence == null || state.Evidence.Count == 0 || state.GlobalConfidenceScore < 30)
            {
                state.FinalReport = "Insufficient supporting evidence found in the knowledge base.";
                return;
            }

            string agentFindings = string.Join("\n\n", state.Evidence.Where(e => !string.IsNullOrEmpty(e.Findings)).Select(e => $"[{e.SourceAgent.ToUpper()}]\n{e.Findings}"));
            string rawChunks = string.Join("\n\n", state.Evidence.Where(e => !string.IsNullOrEmpty(e.ChunkText)).Select(e => e.ChunkText));

            string prompt = $@"
You are a Report Generation Agent.
Using the following agent findings and raw evidence, generate a response to the user's query.
Format the output in clean Markdown. Do NOT include markdown code blocks around the entire response.

RESPONSE STYLE:
- Classify the user query into one of four types and adapt your answer length:
  - TYPE A (Quick Fact): e.g. ""What is X?"". 1-4 sentences maximum. No headings, sections, or bullet lists unless necessary. Answer the core question directly in the very first sentence.
  - TYPE B (Summary Request): e.g. ""Summarize X"". 1 short paragraph OR maximum 5 bullet points.
  - TYPE C (Detailed Explanation): e.g. ""Explain in detail"". Structured answer allowed. Moderate detail.
  - TYPE D (Report / Research): e.g. ""Generate a report"". Full sections, detailed reasoning, long-form output.
- DEFAULT BEHAVIOR: If classification is uncertain, ALWAYS choose the shorter answer. Never choose report mode.
- NEVER generate ""Executive Summary"", ""Overview"", ""Recommendations"", ""Key Findings"", ""Conclusion"", ""Missing Information"", ""Risks"", or ""Next Steps"" unless explicitly requested by the user.

INLINE CITATIONS (CRITICAL):
- You MUST cite evidence using bracketed inline numbers that correspond to the provided source reference IDs.
- Do NOT generate a manual ""Sources:"" or ""References:"" list or dump large source sections at the bottom of your response. The UI handles that automatically.

If the confidence score is below 40, you MUST append this exact warning to the very end of the report:
Confidence: Low. Verification recommended.

If the confidence score is between 40 and 60, you MUST append this exact warning to the very end of the report:
Confidence: Medium

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
