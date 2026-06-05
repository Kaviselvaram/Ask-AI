using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using backend.Models;

namespace backend.Agents;

public class ResearchAgent : IAgent
{
    private readonly Kernel _kernel;

    public string Name => "Research";

    public ResearchAgent(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("RESEARCH AGENT STARTED");

        string promptTemplate = await File.ReadAllTextAsync("Prompts/ResearchAgentPrompt.txt", cancellationToken);
        
        var arguments = new KernelArguments()
        {
            ["query"] = context.Query,
            ["context"] = context.RetrievedContext
        };

        var promptSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(AgentResult),
            Temperature = 0.2, 
            MaxTokens = 1500
        };

        var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, templateFormat: "semantic-kernel", cancellationToken: cancellationToken);
        
        string resultJson = result.GetValue<string>() ?? "{}";

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var agentResult = JsonSerializer.Deserialize<AgentResult>(resultJson, options);
            
            if (agentResult != null)
            {
                Console.WriteLine("RESEARCH AGENT COMPLETED");
                return agentResult with { AgentName = Name };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Research Agent] FAILED: {ex.Message}");
        }

        return new AgentResult(Name, "Research could not be completed.", 0.0);
    }
}
