using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;

string envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2) Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

string connStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
Console.WriteLine($"DB Conn: {connStr}");

var builder = Kernel.CreateBuilder();
builder.AddOpenAITextEmbeddingGeneration("text-embedding-3-small", Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"), endpoint: Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));

var kernel = builder.Build();
var embeddingService = kernel.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

string query = "what is book review?";
var embeddings = embeddingService.GenerateAsync(new[] { query }).Result;
var qEmbed = embeddings.First().Vector.ToArray();

using var conn = new SqlConnection(connStr);
conn.Open();
using var cmd = new SqlCommand("SELECT Id, ChunkText, Embedding FROM Chunks", conn);
using var reader = cmd.ExecuteReader();

double Cosine(float[] vA, float[] vB) {
    double dot = 0, magA = 0, magB = 0;
    for (int i=0; i<vA.Length; i++) { dot += vA[i]*vB[i]; magA += vA[i]*vA[i]; magB += vB[i]*vB[i]; }
    return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
}

var list = new List<(int id, double sim, string text)>();
while (reader.Read()) {
    float[] cEmbed = reader.GetString(2).Split(',').Select(float.Parse).ToArray();
    list.Add((reader.GetInt32(0), Cosine(qEmbed, cEmbed), reader.GetString(1)));
}

foreach(var item in list.OrderByDescending(x => x.sim).Take(10)) {
    Console.WriteLine($"ID: {item.id}, Sim: {item.sim:F4}, Text: {item.text.Substring(0, Math.Min(50, item.text.Length))}");
}
