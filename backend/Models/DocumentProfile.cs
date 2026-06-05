using System.Collections.Generic;

namespace backend.Models;

public class DocumentProfile
{
    public string FileName { get; set; } = string.Empty;
    public List<string> Topics { get; set; } = new();
    public List<string> Entities { get; set; } = new();
}
