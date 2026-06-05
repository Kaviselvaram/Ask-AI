using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using backend.Models;

namespace backend.Services;

public class WorkspaceRelationshipEngine : IWorkspaceRelationshipEngine
{
    private readonly Kernel _kernel;

    public WorkspaceRelationshipEngine(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<WorkspaceSummary> BuildWorkspaceIntelligenceAsync(string context, string userQuery, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("RELATIONSHIPS BUILT"); // User specified logging
        
        try
        {
            string promptTemplate = await File.ReadAllTextAsync("Prompts/WorkspaceEnginePrompt.txt", cancellationToken);
            
            var promptSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                ResponseFormat = typeof(WorkspaceSummary),
                Temperature = 0.2, 
                MaxTokens = 2500
            };

            var arguments = new KernelArguments(promptSettings)
            {
                ["context"] = context,
                ["query"] = userQuery
            };

            var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, templateFormat: "semantic-kernel", cancellationToken: cancellationToken);
            
            string resultJson = result.GetValue<string>() ?? "{}";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var summary = JsonSerializer.Deserialize<WorkspaceSummary>(resultJson, options);
            
            if (summary != null)
            {
                return summary;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("WORKSPACE ENGINE FAILED");
            Console.WriteLine($"[Workspace Engine Error]: {ex.Message}");
        }

        return new WorkspaceSummary();
    }
}
