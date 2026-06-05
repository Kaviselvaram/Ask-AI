using System.Collections.Generic;

namespace backend.Models;

public class WorkspaceEntity
{
    public string EntityName { get; set; } = string.Empty;
    public List<string> DocumentIds { get; set; } = new();
}
