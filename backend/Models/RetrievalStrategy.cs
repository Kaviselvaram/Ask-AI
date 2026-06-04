namespace backend.Models
{
    public record RetrievalStrategy(
        int TopChunks,
        bool UseVectorSearch
    );
}
