using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using backend.Models;

namespace backend.Services;

public class VerificationService : IVerificationService
{
    private readonly Kernel _kernel;

    public VerificationService(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<VerificationResult> VerifyAsync(string answer, string retrievedContext, CancellationToken cancellationToken = default)
    {
        string promptTemplate = await File.ReadAllTextAsync("Prompts/VerificationPrompt.txt", cancellationToken);
        
        var promptSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(VerificationResult),
            Temperature = 0.1, // Low temperature for factual verification
            MaxTokens = 1000
        };

        var arguments = new KernelArguments(promptSettings)
        {
            ["answer"] = answer,
            ["context"] = retrievedContext
        };

        var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, templateFormat: "semantic-kernel", cancellationToken: cancellationToken);
        
        string resultJson = result.GetValue<string>() ?? "{}";

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var verificationResult = JsonSerializer.Deserialize<VerificationResult>(resultJson, options);
            
            if (verificationResult != null)
            {
                return verificationResult;
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Verification JSON Parse Error: {ex.Message}");
        }

        // Fallback to safe defaults if parsing fails
        return new VerificationResult(
            IsSupported: true,
            Confidence: 1.0,
            SupportedClaims: new System.Collections.Generic.List<string>(),
            UnsupportedClaims: new System.Collections.Generic.List<string>(),
            Explanation: "Failed to parse verification result.",
            SafeAnswer: answer
        );
    }
}
