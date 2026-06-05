namespace backend.Models;

public record VerificationResult(
    bool IsSupported,
    double Confidence,
    List<string> SupportedClaims,
    List<string> UnsupportedClaims,
    string Explanation,
    string SafeAnswer
);
