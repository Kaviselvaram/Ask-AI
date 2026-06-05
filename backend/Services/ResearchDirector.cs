using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using backend.Models;
using backend.Orchestration;
using backend.Agents;

namespace backend.Services;

public class ResearchDirector : IResearchDirector
{
    private readonly IAgentOrchestrator _agentOrchestrator;
    private readonly IInsightEngine _insightEngine;
    private readonly IResearchEvidenceCollector _evidenceCollector;
    private readonly Kernel _kernel;

    public ResearchDirector(
        IAgentOrchestrator agentOrchestrator,
        IInsightEngine insightEngine,
        IResearchEvidenceCollector evidenceCollector,
        Kernel kernel)
    {
        _agentOrchestrator = agentOrchestrator;
        _insightEngine = insightEngine;
        _evidenceCollector = evidenceCollector;
        _kernel = kernel;
    }

    public async Task<(ResearchPlan Plan, List<SourceInfo> Sources)> ExecuteResearchAsync(string connectionString, string userQuery, string conversationHistory, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("RESEARCH DIRECTOR STARTED");
        Console.WriteLine("RESEARCH PLAN CREATED");

        // 1. Gather Evidence
        var (evidenceContext, collectedSources, isSufficient) = await _evidenceCollector.CollectEvidenceAsync(connectionString, userQuery);
        
        if (!isSufficient)
        {
            Console.WriteLine("RESEARCH DIRECTOR FAILED: Insufficient Evidence");
            return (new ResearchPlan { Objective = "Analyze Data", Findings = new List<string> { "Insufficient evidence available to generate reliable findings." } }, new List<SourceInfo>());
        }

        // 2. Parallel Evidence Collection
        var agentContext = new AgentContext(
            Query: userQuery,
            Intent: "research comparison", // Forces both Research and Comparison agents
            ExecutionPlan: new ExecutionPlan("Aggregate Research", "Comprehensive", new List<string>(), 5, true, true, false),
            RetrievedContext: evidenceContext,
            ConversationContext: conversationHistory
        );

        Task<string> agentsTask = _agentOrchestrator.ExecuteAsync(agentContext, cancellationToken);
        Task<InsightResult> insightTask = _insightEngine.AnalyzeAsync(evidenceContext, userQuery, cancellationToken);

        await Task.WhenAll(agentsTask, insightTask);

        string agentsOutput = agentsTask.Result ?? "No agent findings.";
        var insightResult = insightTask.Result;

        Console.WriteLine("EVIDENCE COLLECTED");

        string aggregatedEvidence = $"--- AGENT FINDINGS ---\n{agentsOutput}\n\n";
        aggregatedEvidence += "--- VAULT INSIGHTS ---\n";
        if (insightResult?.Themes?.Count > 0) aggregatedEvidence += $"Themes: {string.Join(", ", insightResult.Themes)}\n";
        if (insightResult?.Contradictions?.Count > 0) aggregatedEvidence += $"Contradictions: {string.Join(", ", insightResult.Contradictions)}\n";
        if (insightResult?.Gaps?.Count > 0) aggregatedEvidence += $"Gaps: {string.Join(", ", insightResult.Gaps)}\n";
        
        // 3. Synthesis Generation
        string promptTemplate = await File.ReadAllTextAsync("Prompts/ResearchDirectorPrompt.txt", cancellationToken);
        var promptSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(ResearchPlan),
            Temperature = 0.2, 
            MaxTokens = 2500
        };

        var arguments = new KernelArguments(promptSettings)
        {
            ["evidence"] = aggregatedEvidence,
            ["query"] = userQuery
        };

        var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, templateFormat: "semantic-kernel", cancellationToken: cancellationToken);
        string resultJson = result.GetValue<string>() ?? "{}";

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        ResearchPlan plan;
        try
        {
            plan = JsonSerializer.Deserialize<ResearchPlan>(resultJson, options) ?? new ResearchPlan();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RESEARCH DIRECTOR FAILED TO PARSE JSON: {ex.Message}");
            plan = new ResearchPlan { Objective = "Analyze Data", Findings = new List<string> { "Failed to parse research data." } };
        }

        // 4. Source Filtering
        var finalSources = new List<SourceInfo>();
        if (plan.EvidenceSources != null && plan.EvidenceSources.Count > 0)
        {
            foreach (var src in collectedSources)
            {
                if (plan.EvidenceSources.Any(e => e.Contains(src.FileName, StringComparison.OrdinalIgnoreCase) || src.FileName.Contains(e, StringComparison.OrdinalIgnoreCase)))
                {
                    finalSources.Add(src);
                }
            }
        }
        
        // Fallback: If no EvidenceSources matched but we generated findings, include all collectedSources
        if (finalSources.Count == 0 && plan.Findings != null && plan.Findings.Count > 0 && !plan.Findings[0].StartsWith("Insufficient"))
        {
            finalSources = collectedSources;
        }

        Console.WriteLine("FINDINGS GENERATED");
        Console.WriteLine("RECOMMENDATIONS GENERATED");
        Console.WriteLine("SOURCES USED");
        foreach(var s in finalSources) Console.WriteLine($"- {s.FileName}");
        Console.WriteLine("FINAL SOURCES RETURNED");
        Console.WriteLine("RESEARCH DIRECTOR COMPLETED");

        return (plan, finalSources);
    }
}
