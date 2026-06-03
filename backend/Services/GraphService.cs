using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace backend.Services
{
    public class GraphService
    {
        private readonly string _connectionString;

        public GraphService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<string> GetGraphContextAsync(List<string> keywords, int? documentId = null)
        {
            if (keywords == null || !keywords.Any())
                return string.Empty;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var likeConditions = new List<string>();
            for (int i = 0; i < keywords.Count; i++)
            {
                likeConditions.Add($"(e1.Name LIKE @k{i} OR e2.Name LIKE @k{i})");
            }

            string kgSql = $@"SELECT TOP 10 e1.Name as Source, r.RelationType, e2.Name as Target 
                              FROM GraphRelationships r 
                              JOIN GraphEntities e1 ON r.SourceEntityId = e1.Id 
                              JOIN GraphEntities e2 ON r.TargetEntityId = e2.Id 
                              WHERE " + string.Join(" OR ", likeConditions);
                              
            if (documentId.HasValue) 
            {
                kgSql += " AND r.DocumentId = @DocId";
            }

            using SqlCommand kgCmd = new SqlCommand(kgSql, connection);
            for (int i = 0; i < keywords.Count; i++)
            {
                kgCmd.Parameters.AddWithValue($"@k{i}", $"%{keywords[i]}%");
            }
            if (documentId.HasValue) 
            {
                kgCmd.Parameters.AddWithValue("@DocId", documentId.Value);
            }

            using SqlDataReader kgReader = await kgCmd.ExecuteReaderAsync();
            var kgTriplets = new List<string>();
            while (await kgReader.ReadAsync())
            {
                kgTriplets.Add($"({kgReader.GetString(0)}) -[{kgReader.GetString(1)}]-> ({kgReader.GetString(2)})");
            }

            return kgTriplets.Any() ? string.Join("\n", kgTriplets) : string.Empty;
        }
    }
}
