using System.Collections.Generic;
using System.Linq;

namespace backend.Models
{
    public class SourceInfo
    {
        public int ReferenceId { get; set; }
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public List<int> Pages { get; set; } = new();
        public string DownloadUrl { get; set; } = string.Empty;

        public string FormattedPages => Pages.Any() ? "Page " + string.Join(", ", Pages.Distinct().OrderBy(p => p)) : "Page 1";
    }
}
