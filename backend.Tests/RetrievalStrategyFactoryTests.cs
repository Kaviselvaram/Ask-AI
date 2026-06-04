using Xunit;
using backend.Models;
using backend.Services;

namespace backend.Tests
{
    public class RetrievalStrategyFactoryTests
    {
        [Fact]
        public void Fact_Returns3Chunks_VectorSearchTrue()
        {
            var factory = new RetrievalStrategyFactory();
            var strategy = factory.GetStrategy(QueryIntent.Fact);
            
            Assert.Equal(3, strategy.TopChunks);
            Assert.True(strategy.UseVectorSearch);
        }

        [Fact]
        public void Summary_Returns5Chunks_VectorSearchTrue()
        {
            var factory = new RetrievalStrategyFactory();
            var strategy = factory.GetStrategy(QueryIntent.Summary);
            
            Assert.Equal(5, strategy.TopChunks);
            Assert.True(strategy.UseVectorSearch);
        }

        [Fact]
        public void SystemQuestion_Returns0Chunks_VectorSearchFalse()
        {
            var factory = new RetrievalStrategyFactory();
            var strategy = factory.GetStrategy(QueryIntent.SystemQuestion);
            
            Assert.Equal(0, strategy.TopChunks);
            Assert.False(strategy.UseVectorSearch);
        }
    }
}
