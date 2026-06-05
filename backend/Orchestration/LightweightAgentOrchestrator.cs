using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Agents;
using backend.Models;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Orchestration;

public class LightweightAgentOrchestrator : IAgentOrchestrator
{
    private readonly IEnumerable<IAgent> _agents;

    public LightweightAgentOrchestrator(IEnumerable<IAgent> agents)
    {
        _agents = agents;
    }

    public async Task<string> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("ORCHESTRATOR STARTED");

        var activeAgents = new List<IAgent>();

        // Routing Rules
        string intent = context.Intent?.ToLowerInvariant() ?? "";
        bool needsResearch = intent.Contains("fact") || intent.Contains("summary") || intent.Contains("research") || intent.Contains("comparison");
        bool needsComparison = intent.Contains("comparison");

        if (intent.Contains("system"))
        {
            Console.WriteLine("ORCHESTRATOR COMPLETED (Skipped for System Questions)");
            return null; // Bypass agents
        }

        if (needsResearch)
        {
            var researchAgent = _agents.FirstOrDefault(a => a.Name == "Research");
            if (researchAgent != null)
            {
                activeAgents.Add(researchAgent);
                Console.WriteLine("AGENT SELECTED: Research");
            }
        }

        if (needsComparison)
        {
            var comparisonAgent = _agents.FirstOrDefault(a => a.Name == "Comparison");
            if (comparisonAgent != null)
            {
                activeAgents.Add(comparisonAgent);
                Console.WriteLine("AGENT SELECTED: Comparison");
            }
        }

        if (activeAgents.Count == 0)
        {
            Console.WriteLine("ORCHESTRATOR COMPLETED (No agents selected)");
            return null;
        }

        string aggregatedFindings = "";
        double totalConfidence = 0;
        int agentsExecuted = 0;

        foreach (var agent in activeAgents)
        {
            try
            {
                var updatedContext = context with { PreviousAgentOutput = aggregatedFindings };
                
                var result = await agent.ExecuteAsync(updatedContext, cancellationToken);
                
                aggregatedFindings += $"\n\n--- {agent.Name} Agent Findings ---\n{result.Output}";
                totalConfidence += result.Confidence;
                agentsExecuted++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AGENT FAILED: {agent.Name} - {ex.Message}");
            }
        }

        Console.WriteLine("ORCHESTRATOR COMPLETED");

        if (agentsExecuted > 0)
        {
            return aggregatedFindings.Trim();
        }

        return null;
    }
}
