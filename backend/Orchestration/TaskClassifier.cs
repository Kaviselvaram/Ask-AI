using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace backend.Orchestration
{
    public class TaskClassifier
    {
        private readonly Kernel _kernel;

        public TaskClassifier(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<TaskClassificationResult> ClassifyTaskAsync(string query)
        {
            string prompt = @"
You are an intent classification engine. Categorize the user's document query into one of the following tasks:
- SimpleRetrieval (e.g. 'what is DA4', 'summarize this document')
- MultiDocumentComparison (e.g. 'compare 2024 and 2025 policies')
- RiskAnalysis (e.g. 'what are the risks in this contract', 'detect contradictions')
- ExecutiveReporting (e.g. 'generate an executive summary of Project Alpha')
- ResearchInvestigation (e.g. 'find all clauses related to termination across all documents')

Return EXACTLY a JSON object in this format, with no markdown formatting or extra text:
{
  ""task_type"": ""[TaskType]"",
  ""confidence"": [0-100],
  ""reasoning"": ""[Brief reasoning]""
}

User Query: {{$input}}";

            var result = await _kernel.InvokePromptAsync(prompt, new() { ["input"] = query });
            string json = result.GetValue<string>()?.Trim() ?? "{}";
            
            // Clean markdown if the LLM adds it
            if (json.StartsWith("```json"))
            {
                json = json.Substring(7, json.Length - 10).Trim();
            }

            try
            {
                var classification = JsonSerializer.Deserialize<TaskClassificationResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return classification ?? new TaskClassificationResult { TaskType = "SimpleRetrieval", Confidence = 100, Reasoning = "Fallback" };
            }
            catch
            {
                return new TaskClassificationResult { TaskType = "SimpleRetrieval", Confidence = 100, Reasoning = "Parse Error Fallback" };
            }
        }
    }

    public class TaskClassificationResult
    {
        public string TaskType { get; set; } = string.Empty;
        public int Confidence { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }
}
