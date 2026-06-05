using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.SemanticKernel;

namespace backend.Services
{
    public class PlannerService : IPlannerService
    {
        private readonly Kernel _kernel;

        public PlannerService(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<ExecutionPlan> CreatePlanAsync(string query, QueryIntent intent)
        {
            Console.WriteLine("PLANNER STARTED");
            
            // Check feature flag - default to true unless explicitly disabled
            string isEnabledStr = Environment.GetEnvironmentVariable("Planner:Enabled") ?? "true";
            if (!bool.TryParse(isEnabledStr, out bool isEnabled) || !isEnabled)
            {
                Console.WriteLine("PLANNER FALLBACK ACTIVATED: Feature flag disabled.");
                return CreateFallbackPlan(query, intent);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));

            try
            {
                string promptPath = Path.Combine(Directory.GetCurrentDirectory(), "Prompts", "PlannerPrompt.txt");
                string promptTemplate = await File.ReadAllTextAsync(promptPath, cts.Token);
                
                var arguments = new KernelArguments 
                { 
                    { "query", query },
                    { "intent", intent.ToString() }
                };
                
                var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, cancellationToken: cts.Token);
                string jsonOutput = result.GetValue<string>()?.Trim() ?? "";

                // Remove markdown blocks if the LLM ignored instructions
                if (jsonOutput.StartsWith("```json"))
                {
                    jsonOutput = jsonOutput.Substring(7);
                    if (jsonOutput.EndsWith("```"))
                    {
                        jsonOutput = jsonOutput.Substring(0, jsonOutput.Length - 3);
                    }
                }
                
                var plan = JsonSerializer.Deserialize<ExecutionPlan>(jsonOutput, options);
                
                if (plan != null)
                {
                    Console.WriteLine("PLANNER COMPLETED");
                    return plan;
                }
                else
                {
                    throw new Exception("Deserialized plan is null");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("PLANNER FAILED: Timeout exceeded 2000ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PLANNER FAILED: {ex.Message}");
            }
            
            Console.WriteLine("PLANNER FALLBACK ACTIVATED: Exception caught.");
            return CreateFallbackPlan(query, intent);
        }

        private ExecutionPlan CreateFallbackPlan(string query, QueryIntent intent)
        {
            int defaultChunks = intent switch
            {
                QueryIntent.Fact => 3,
                QueryIntent.Summary => 5,
                QueryIntent.Comparison => 10,
                QueryIntent.Research => 15,
                QueryIntent.SystemQuestion => 0,
                _ => 5
            };

            bool isComparison = intent == QueryIntent.Comparison;
            bool isMulti = intent == QueryIntent.Comparison || intent == QueryIntent.Research;

            return new ExecutionPlan(
                Goal: query,
                Strategy: "FallbackStrategy",
                Steps: new List<string> { "Retrieve fallback chunks", "Generate response" },
                RecommendedChunkCount: defaultChunks,
                RequiresComparison: isComparison,
                RequiresMultiDocumentReasoning: isMulti,
                RequiresKnowledgeGraph: false
            );
        }
    }
}
