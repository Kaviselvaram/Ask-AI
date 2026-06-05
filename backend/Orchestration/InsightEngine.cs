using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using backend.Models;

namespace backend.Orchestration;

public class InsightEngine : IInsightEngine
{
    private readonly Kernel _kernel;

    public InsightEngine(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<InsightResult> AnalyzeAsync(string context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("INSIGHT ENGINE STARTED");

        try
        {
            string promptTemplate = await File.ReadAllTextAsync("Prompts/InsightEnginePrompt.txt", cancellationToken);
            
            var arguments = new KernelArguments()
            {
                ["context"] = context
            };

            var promptSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                ResponseFormat = typeof(InsightResult),
                Temperature = 0.2, 
                MaxTokens = 1500
            };

            var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, templateFormat: "semantic-kernel", cancellationToken: cancellationToken);
            
            string resultJson = result.GetValue<string>() ?? "{}";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var insightResult = JsonSerializer.Deserialize<InsightResult>(resultJson, options);
            
            if (insightResult != null)
            {
                if (insightResult.Themes?.Count > 0) Console.WriteLine("THEMES DETECTED");
                if (insightResult.Contradictions?.Count > 0) Console.WriteLine("CONTRADICTIONS DETECTED");
                if (insightResult.Gaps?.Count > 0) Console.WriteLine("GAPS DETECTED");
                if (insightResult.Duplicates?.Count > 0) Console.WriteLine("DUPLICATES DETECTED");
                
                Console.WriteLine("INSIGHT ENGINE COMPLETED");
                return insightResult;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("INSIGHT ENGINE FAILED");
            Console.WriteLine($"[Insight Engine Error]: {ex.Message}");
        }

        return new InsightResult(new(), new(), new(), new(), 0.0);
    }
}
