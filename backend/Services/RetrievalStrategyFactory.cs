using backend.Models;

namespace backend.Services
{
    public class RetrievalStrategyFactory
    {
        public RetrievalStrategy GetStrategy(QueryIntent intent)
        {
            return intent switch
            {
                QueryIntent.Fact => new RetrievalStrategy(3, true),
                QueryIntent.Summary => new RetrievalStrategy(5, true),
                QueryIntent.Comparison => new RetrievalStrategy(10, true),
                QueryIntent.Research => new RetrievalStrategy(15, true),
                QueryIntent.DocumentQuestion => new RetrievalStrategy(8, true),
                QueryIntent.SystemQuestion => new RetrievalStrategy(0, false),
                QueryIntent.Unknown => new RetrievalStrategy(5, true),
                _ => new RetrievalStrategy(5, true)
            };
        }
    }
}
