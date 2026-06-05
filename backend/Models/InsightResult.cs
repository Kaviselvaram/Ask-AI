using System.Collections.Generic;

namespace backend.Models;

public record InsightResult(
    List<string> Themes,
    List<string> Contradictions,
    List<string> Gaps,
    List<string> Duplicates,
    double Confidence
);
