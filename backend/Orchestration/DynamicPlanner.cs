using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace backend.Orchestration
{
    public class DynamicPlanner
    {
        private readonly Kernel _kernel;

        public DynamicPlanner(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<List<string>> GeneratePlanAsync(string query, string intent)
        {
            string prompt = $@"
You are an expert Task Planner for a Multi-Agent Document Intelligence System.
The user's query has been classified as: {intent}

Based on this intent, generate a sequential array of agent steps to execute.
Available Agents/Steps:
- ""research"": Search the vault to find relevant chunks across documents.
- ""comparison"": Compare multiple retrieved policies or documents.
- ""risk_analysis"": Detect contradictions, missing info, and risky clauses.
- ""executive_summary"": Generate an executive report and action items.

Always end the plan with ""verification"" to check factual accuracy, followed by ""report_generation"" to format the final output.

Return EXACTLY a JSON array of strings, e.g.:
[
  ""research"",
  ""comparison"",
  ""verification"",
  ""report_generation""
]

User Query: {{$input}}";

            var result = await _kernel.InvokePromptAsync(prompt, new() { ["input"] = query });
            string json = result.GetValue<string>()?.Trim() ?? "[]";

            if (json.StartsWith("```json"))
            {
                json = json.Substring(7, json.Length - 10).Trim();
            }

            try
            {
                var plan = JsonSerializer.Deserialize<List<string>>(json);
                return plan ?? new List<string> { "research", "verification", "report_generation" };
            }
            catch
            {
                return new List<string> { "research", "verification", "report_generation" };
            }
        }
    }
}
