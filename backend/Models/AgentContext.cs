namespace backend.Models;

public record AgentContext(
    string Query,
    string Intent,
    ExecutionPlan ExecutionPlan,
    string RetrievedContext,
    string ConversationContext,
    string PreviousAgentOutput = ""
);
