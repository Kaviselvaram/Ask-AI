namespace backend.Models
{
    public record SourceInfo(
        int DocumentId,
        string FileName,
        int PageNumber,
        string DownloadUrl
    );
}
