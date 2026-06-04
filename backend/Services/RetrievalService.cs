using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;
using backend.Models;

namespace backend.Services
{
    public class RetrievalService
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly string _connectionString;

        public RetrievalService(IEmbeddingGenerator<string, Embedding<float>> embeddingService, string connectionString)
        {
            _embeddingService = embeddingService;
            _connectionString = connectionString;
        }

        public double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }

        public async Task<(List<string> Chunks, List<SourceInfo> Sources, double ConfidenceScore)> GetRelevantChunksAsync(
            string query,
            int? documentId = null)
        {
            var embeddings = await _embeddingService.GenerateAsync(new[] { query });
            var questionEmbedding = embeddings.First().Vector.ToArray();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql = @"
                SELECT
                    c.ChunkText,
                    c.Embedding,
                    (SELECT TOP 1 d2.FileName FROM Documents d2 JOIN DocumentChunkMapping m2 ON d2.Id = m2.DocumentId WHERE m2.ChunkId = c.Id AND d2.Status = 'Latest') as FileName,
                    (SELECT TOP 1 m2.PageNumber FROM Documents d2 JOIN DocumentChunkMapping m2 ON d2.Id = m2.DocumentId WHERE m2.ChunkId = c.Id AND d2.Status = 'Latest') as PageNumber,
                    (SELECT TOP 1 d2.Id FROM Documents d2 JOIN DocumentChunkMapping m2 ON d2.Id = m2.DocumentId WHERE m2.ChunkId = c.Id AND d2.Status = 'Latest') as DocumentId
                FROM Chunks c
                WHERE EXISTS (
                    SELECT 1 FROM DocumentChunkMapping m 
                    JOIN Documents d ON m.DocumentId = d.Id 
                    WHERE m.ChunkId = c.Id AND d.Status = 'Latest' ";

            if (documentId.HasValue)
            {
                sql += " AND d.Id = @DocumentId ";
            }
            
            sql += ")";

            using SqlCommand command = new SqlCommand(sql, connection);
            if (documentId.HasValue)
            {
                command.Parameters.AddWithValue("@DocumentId", documentId.Value);
            }

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            var chunks = new List<(string Text, double Score)>();
            var sources = new List<SourceInfo>();

            while (await reader.ReadAsync())
            {
                try
                {
                    string chunkText = reader.GetString(0);
                    string embeddingText = reader.GetString(1);
                    string fileName = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2);
                    int pageNumber = reader.IsDBNull(3) ? 1 : reader.GetInt32(3);
                    int docId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);

                    float[] chunkEmbedding = embeddingText.Split(',').Select(float.Parse).ToArray();

                    if (chunkEmbedding.Length != questionEmbedding.Length)
                        continue;

                    double similarity = CosineSimilarity(questionEmbedding, chunkEmbedding);

                    string formattedChunk = $"[Source: {fileName} | Page: {pageNumber}]\n{chunkText}";
                    chunks.Add((formattedChunk, similarity));

                    if (docId != 0 && fileName != "Unknown")
                    {
                        sources.Add(new SourceInfo(docId, fileName, pageNumber, $"/download/{docId}"));
                    }
                }
                catch
                {
                    continue;
                }
            }

            var topChunks = chunks.OrderByDescending(x => x.Score).Take(5).ToList();
            double avgScore = topChunks.Any() ? topChunks.Average(x => x.Score) : 0;
            double confidence = avgScore * 100.0;

            return (topChunks.Select(x => x.Text).ToList(), sources.Distinct().ToList(), confidence);
        }
    }
}
