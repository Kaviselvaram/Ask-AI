using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using backend.Models;

namespace backend.Agents;

public class ComparisonAgent : IAgent
{
    private readonly Kernel _kernel;

    public string Name => "Comparison";

    public ComparisonAgent(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("COMPARISON AGENT STARTED");
        Console.WriteLine("COMPARISON AGENT INPUT:\n" + context.RetrievedContext + "\nPREVIOUS OUTPUT:\n" + context.PreviousAgentOutput);

        string promptTemplate = await File.ReadAllTextAsync("Prompts/ComparisonAgentPrompt.txt", cancellationToken);
        
        var arguments = new KernelArguments()
        {
            ["query"] = context.Query,
            ["context"] = context.RetrievedContext,
            ["previous_output"] = context.PreviousAgentOutput
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
                Console.WriteLine("COMPARISON AGENT COMPLETED");
                Console.WriteLine("COMPARISON AGENT OUTPUT:\n" + agentResult.Output);
                return agentResult with { AgentName = Name };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Comparison Agent] FAILED: {ex.Message}");
        }

        return new AgentResult(Name, "Comparison could not be completed.", 0.0);
    }
}
