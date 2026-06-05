using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;
using backend.Models;

namespace backend.Services;

public class ResearchEvidenceCollector : IResearchEvidenceCollector
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;

    public ResearchEvidenceCollector(IEmbeddingGenerator<string, Embedding<float>> embeddingService)
    {
        _embeddingService = embeddingService;
    }

    private double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;
        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }
        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }

    public async Task<(string EvidenceContext, List<SourceInfo> CollectedSources, bool IsSufficient)> CollectEvidenceAsync(string connectionString, string userQuery)
    {
        Console.WriteLine("RESEARCH QUERY DETECTED");
        Console.WriteLine("COLLECTING EVIDENCE FOR RESEARCH DIRECTOR...");

        var embeddings = await _embeddingService.GenerateAsync(new[] { userQuery });
        var questionEmbedding = embeddings.First().Vector.ToArray();

        using var connection = new SqlConnection(connectionString);
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
                WHERE m.ChunkId = c.Id AND d.Status = 'Latest'
            )";

        using SqlCommand command = new SqlCommand(sql, connection);
        using SqlDataReader reader = await command.ExecuteReaderAsync();
        
        var chunksData = new List<(string Text, double Score, int DocId, string FileName, int PageNumber)>();
        var queryWords = userQuery.ToLowerInvariant().Split(new[] { ' ', '?', '.', ',' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();

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
                if (chunkEmbedding.Length != questionEmbedding.Length) continue;

                double similarity = CosineSimilarity(questionEmbedding, chunkEmbedding);

                if (queryWords.Any(w => fileName.ToLowerInvariant().Contains(w))) similarity += 0.05;

                chunksData.Add((chunkText, similarity, docId, fileName, pageNumber));
            }
            catch { continue; }
        }

        // 1. Fetch top 30 highly relevant chunks
        var highQualityChunks = chunksData.Where(x => x.Score > 0.25)
                                          .OrderByDescending(x => x.Score)
                                          .Take(30)
                                          .ToList();

        // 2. Group by Topic / Document
        var groupedEvidence = highQualityChunks.GroupBy(x => new { x.DocId, x.FileName }).ToList();
        
        Console.WriteLine($"DOCUMENTS SELECTED: {groupedEvidence.Count}");
        Console.WriteLine($"DOCUMENTS ANALYZED: {groupedEvidence.Count}");

        // 3. Minimum Requirements check
        var finalGroupedChunks = new Dictionary<string, List<string>>();
        var sources = new List<SourceInfo>();
        int totalValidChunks = 0;

        foreach (var group in groupedEvidence)
        {
            var uniqueChunks = group.Select(c => c.Text).Distinct().ToList(); // Deduplicate evidence
            
            // Research Requirement: minimum 3 chunks per relevant document, OR the document is explicitly named in the query
            bool isExplicitlyNamed = queryWords.Any(w => group.Key.FileName.ToLowerInvariant().Contains(w));
            
            if (uniqueChunks.Count >= 3 || isExplicitlyNamed)
            {
                finalGroupedChunks[group.Key.FileName] = uniqueChunks;
                totalValidChunks += uniqueChunks.Count;

                sources.Add(new SourceInfo
                {
                    ReferenceId = sources.Count + 1,
                    DocumentId = group.Key.DocId,
                    FileName = group.Key.FileName,
                    DownloadUrl = $"/download/{group.Key.DocId}",
                    Pages = group.Select(g => g.PageNumber).Distinct().Take(5).ToList()
                });
            }
        }

        Console.WriteLine($"CHUNKS COLLECTED: {totalValidChunks}");
        Console.WriteLine($"EVIDENCE ITEMS COLLECTED: {totalValidChunks}");

        // 4. Verify Total Evidence Count
        if (totalValidChunks < 3 || sources.Count == 0)
        {
            return ("", new List<SourceInfo>(), false);
        }

        // 5. Build Evidence Context
        var contextLines = new List<string>();
        foreach (var kvp in finalGroupedChunks)
        {
            contextLines.Add($"--- Document: {kvp.Key} ---");
            contextLines.AddRange(kvp.Value.Select(c => $"* {c}"));
            contextLines.Add("");
        }

        return (string.Join("\n", contextLines), sources, true);
    }
}
