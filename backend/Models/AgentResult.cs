namespace backend.Models;

public record AgentResult(
    string AgentName,
    string Output,
    double Confidence
);
