#pragma warning disable SKEXP0010

using dotenv.net;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using System.Linq;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Text.Json;
// using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using backend.Models;
using backend.Agents;
using backend.Orchestration;
using backend.Services;



List<string> ChunkText(
    string text,
    int size = 500
)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return new List<string>();
    }

    var words = text.Split(
        new[] { ' ', '\r', '\n', '\t' },
        StringSplitOptions.RemoveEmptyEntries
    );

    var chunks = new List<string>();

    for (int i = 0; i < words.Length; i += size)
    {
        chunks.Add(
            string.Join(
                " ",
                words.Skip(i).Take(size)
            )
        );
    }

    return chunks;
}


DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddCors();

var app = builder.Build();

// app.UseAntiforgery();


app.UseSwagger();

app.UseSwaggerUI();

app.UseCors(policy =>
    policy.AllowAnyOrigin()
          .AllowAnyHeader()
          .AllowAnyMethod());

//
// AZURE OPENAI CONFIG
//

string endpoint =
    Environment.GetEnvironmentVariable(
        "AZURE_OPENAI_ENDPOINT")!;

string apiKey =
    Environment.GetEnvironmentVariable(
        "AZURE_OPENAI_KEY")!;

string deployment =
    Environment.GetEnvironmentVariable(
        "AZURE_OPENAI_DEPLOYMENT")!;
string embeddingDeployment =
    Environment.GetEnvironmentVariable(
        "AZURE_OPENAI_EMBEDDING_DEPLOYMENT")!;


string connectionString =
    Environment.GetEnvironmentVariable(
        "SQL_CONNECTION_STRING")!
    + ";Pooling=false";



Console.WriteLine(
    "Connected to Azure SQL!"
);

Console.WriteLine(
    "SQL Connection String Loaded"
);
//
// CREATE SEMANTIC KERNEL
//

var kernelBuilder = Kernel.CreateBuilder();


kernelBuilder.Services.AddSingleton<
    IFunctionInvocationFilter,
    FunctionFilter>();

kernelBuilder.AddAzureOpenAIChatCompletion(
    deployment,
    endpoint,
    apiKey
);
kernelBuilder.AddAzureOpenAIEmbeddingGenerator(
    embeddingDeployment,
    endpoint,
    apiKey
);

Kernel kernel = kernelBuilder.Build();
var embeddingService =
    kernel.Services.GetRequiredService<
        Microsoft.Extensions.AI.IEmbeddingGenerator<
            string,
            Microsoft.Extensions.AI.Embedding<float>
        >
    >();
kernel.Plugins.AddFromType<StartupPlugin>();


var chatService =
    kernel.GetRequiredService<IChatCompletionService>();

// Agent Orchestration Setup
var retrievalService = new RetrievalService(embeddingService, connectionString);
var graphService = new GraphService(connectionString);

var classifier = new TaskClassifier(kernel);
var planner = new DynamicPlanner(kernel);
var reportGenerator = new ReportGenerator(kernel);

var agents = new List<BaseAgent>
{
    new ResearchAgent(retrievalService, graphService),
    new ComparisonAgent(kernel),
    new RiskAnalysisAgent(kernel),
    new ExecutiveSummaryAgent(kernel),
    new VerificationAgent(kernel)
};

var orchestrator = new AgentOrchestrator(classifier, planner, reportGenerator, agents);

//
// CHAT ENDPOINT
//

app.MapPost("/chat",
async (ChatRequest request) =>
{
    try
    {
    Console.WriteLine("STEP 1");

using SqlConnection connection =
    new SqlConnection(connectionString);

Console.WriteLine("STEP 2");

Console.WriteLine("ABOUT TO OPEN SQL");
connection.Open();
Console.WriteLine("SQL OPENED");

Console.WriteLine("STEP 3");

string query =
    request.message.ToLower();

bool shouldSkipCache =
    query.Contains("another")
    ||
    query.Contains("new")
    ||
    query.Contains("different")
    ||
    query.Contains("idea")
    ||
    query.Contains("generate")
    ||
    query.Contains("create")
    ||
    query.Contains("suggest");

Console.WriteLine($"QUERY: {request.message}");
Console.WriteLine($"SKIP CACHE: {shouldSkipCache}");

    string selectSql =
        @"SELECT TOP 1 Answer
        FROM QuestionCache
        WHERE Question = @Question";

    if (request.documentId.HasValue)
    {
        selectSql += " AND DocumentId = @DocumentId";
    }
    else
    {
        selectSql += " AND DocumentId IS NULL";
    }

    using SqlCommand selectCommand =
        new SqlCommand(
            selectSql,
            connection
        );

    selectCommand.Parameters.AddWithValue(
        "@Question",
        request.message
    );

    if (request.documentId.HasValue)
    {
        selectCommand.Parameters.AddWithValue(
            "@DocumentId",
            request.documentId.Value
        );
    }

    var cachedResult =
        selectCommand.ExecuteScalar();
    Console.WriteLine("STEP 4");
    Console.WriteLine("ABOUT TO LOAD RewritePrompt.txt");

    if (
    !shouldSkipCache &&
    cachedResult != null
)
    {
        Console.WriteLine(
            "CACHE HIT"
        );

        return Results.Ok(new
        {
            result =
                cachedResult.ToString()
        });
    }

    // Feature 5: Query Rewriter
    string rewritePrompt = await File.ReadAllTextAsync("Prompts/RewritePrompt.txt");
    Console.WriteLine("RewritePrompt LOADED");  
    Console.WriteLine("ABOUT TO CALL KERNEL");
    var rewriteResult = await kernel.InvokePromptAsync(rewritePrompt, new() { ["input"] = request.message });
    Console.WriteLine("KERNEL CALL SUCCESS");
    string rewrittenQuery = rewriteResult.GetValue<string>()?.Trim() ?? request.message;
    Console.WriteLine($"Original Query: {request.message} | Rewritten: {rewrittenQuery}");

    // NEW: CLASSIFICATION ROUTING
    var classification = await classifier.ClassifyTaskAsync(rewrittenQuery);
    if (classification.TaskType != "SimpleRetrieval" && classification.Confidence > 60)
    {
        var agentState = await orchestrator.ExecuteAsync(request.message, request.documentId);
        
        return Results.Ok(new {
            result = agentState.FinalReport,
            chunksRetrieved = agentState.Evidence.Count,
            similarityScore = agentState.GlobalConfidenceScore / 100.0
        });
    }

Console.WriteLine("ABOUT TO GENERATE EMBEDDINGS");

var embeddings =
    await embeddingService.GenerateAsync(
        new[]
        {
            rewrittenQuery
        }
    );

Console.WriteLine("EMBEDDING SUCCESS");
Console.WriteLine("STEP 5");

var embedding =
    embeddings.First();

float[] currentEmbedding =
    embedding.Vector.ToArray();

Console.WriteLine("ABOUT TO RETRIEVE RELEVANT CHUNKS");
var (relevantChunks,sources, confidenceScore) =
        GetRelevantChunks(
            currentEmbedding,
            connection,
            request.documentId
        );
Console.WriteLine($"RETRIEVED {relevantChunks.Count} RELEVANT CHUNKS WITH CONFIDENCE {confidenceScore}");

// Removed hard short-circuit; we now trust the LLM to refuse based on system prompt.

    // Feature 3: Knowledge Graph Retrieval
    string kgContext = "";
    try
    {
        string extractPrompt = await File.ReadAllTextAsync("Prompts/ExtractPrompt.txt");
        var extractResult = await kernel.InvokePromptAsync(extractPrompt, new() { ["input"] = rewrittenQuery });
        string keywordsRaw = extractResult.GetValue<string>() ?? "";
        var keywords = keywordsRaw.Split(',').Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).ToList();
        
        if (keywords.Any())
        {
            var likeConditions = new List<string>();
            for(int i=0; i<keywords.Count; i++) likeConditions.Add($"(e1.Name LIKE @k{i} OR e2.Name LIKE @k{i})");
            
            string kgSql = $@"SELECT TOP 10 e1.Name as Source, r.RelationType, e2.Name as Target 
                              FROM GraphRelationships r 
                              JOIN GraphEntities e1 ON r.SourceEntityId = e1.Id 
                              JOIN GraphEntities e2 ON r.TargetEntityId = e2.Id 
                              WHERE " + string.Join(" OR ", likeConditions);
            if (request.documentId.HasValue) kgSql += " AND r.DocumentId = @DocId";
            
            using SqlCommand kgCmd = new SqlCommand(kgSql, connection);
            for(int i=0; i<keywords.Count; i++) kgCmd.Parameters.AddWithValue($"@k{i}", $"%{keywords[i]}%");
            if (request.documentId.HasValue) kgCmd.Parameters.AddWithValue("@DocId", request.documentId.Value);
            
            using SqlDataReader kgReader = kgCmd.ExecuteReader();
            var kgEdges = new List<string>();
            while(kgReader.Read()) kgEdges.Add($"{kgReader.GetString(0)} [{kgReader.GetString(1)}] {kgReader.GetString(2)}");
            
            if (kgEdges.Any()) kgContext = "Knowledge Graph Context:\n" + string.Join("\n", kgEdges) + "\n\n";
        }
    }
    catch (Exception ex)
    {
         Console.WriteLine("KG Error: " + ex.Message);
    }

    string confidenceWarning = confidenceScore > 0 && confidenceScore <= 85 
        ? $"\n\n> [!WARNING]\n> **Confidence Score: {confidenceScore:F1}%**. Verification recommended." 
        : (confidenceScore > 85 ? $"\n\n*(Confidence Score: {confidenceScore:F1}%)*" : "");

    string context = kgContext +
        string.Join(
            "\n\n",
            relevantChunks
    );

string embeddingString =
    string.Join(
        ",",
        currentEmbedding
    );

string semanticSql =
    @"SELECT
        QuestionEmbedding,
        Answer
      FROM QuestionCache
      WHERE QuestionEmbedding IS NOT NULL";

if (request.documentId.HasValue)
{
    semanticSql += " AND DocumentId = @DocumentId";
}
else
{
    semanticSql += " AND DocumentId IS NULL";
}

using SqlCommand semanticCommand =
    new SqlCommand(
        semanticSql,
        connection
    );

if (request.documentId.HasValue)
{
    semanticCommand.Parameters.AddWithValue(
        "@DocumentId",
        request.documentId.Value
    );
}

Console.WriteLine("ABOUT TO QUERY SEMANTIC CACHE");
double highestSimilarity = 0;
string? bestAnswer = null;

using (SqlDataReader reader = semanticCommand.ExecuteReader())
{
    Console.WriteLine("SEMANTIC CACHE READER OPENED");

    while (reader.Read())
    {
        try
        {
            string storedEmbeddingText =
                reader.GetString(0);

            string storedAnswer =
                reader.GetString(1);

            float[] storedEmbedding =
                storedEmbeddingText
                    .Split(',')
                    .Select(float.Parse)
                    .ToArray();

            if (
                storedEmbedding.Length !=
                currentEmbedding.Length
            )
            {
                continue;
            }

            double similarity =
                CosineSimilarity(
                    currentEmbedding,
                    storedEmbedding
                );

            if (
                similarity >
                highestSimilarity
            )
            {
                highestSimilarity =
                    similarity;

                bestAnswer =
                    storedAnswer;
            }
        }
        catch
        {
            continue;
        }
    }
}

Console.WriteLine(
    $"Best Similarity: {highestSimilarity}"
);


if (
    !shouldSkipCache &&
    highestSimilarity > 0.85
)
{
    Console.WriteLine(
        "SEMANTIC CACHE HIT"
    );

    return Results.Ok(new
    {
        result = bestAnswer,
        chunksRetrieved = 1,
        similarityScore = highestSimilarity
    });
}

    // Feature 6: Inject Available Documents Manifest
    string availableDocs = "No documents currently in the vault.";
    try 
    {
        using SqlCommand docsCmd = new SqlCommand("SELECT FileName, Version FROM Documents WHERE Status = 'Latest'", connection);
        using SqlDataReader docsReader = docsCmd.ExecuteReader();
        var docList = new List<string>();
        while(docsReader.Read())
        {
            docList.Add($"- {docsReader.GetString(0)} (v{docsReader.GetInt32(1)})");
        }
        if (docList.Count > 0)
        {
            availableDocs = string.Join("\n", docList);
        }
    } 
    catch { }

    var history = new ChatHistory();
    string systemPrompt = await File.ReadAllTextAsync("Prompts/ChatPrompt.txt");
    history.AddSystemMessage(systemPrompt);

    history.AddUserMessage($@"
Knowledge Base Context & Evidence:

{context}

[SYSTEM MANIFEST - AVAILABLE DOCUMENTS IN VAULT]
{availableDocs}

User Query:

{request.message}
");



Console.WriteLine("CALLING AZURE OPENAI...");
    OpenAIPromptExecutionSettings settings =
        new()
        {
            FunctionChoiceBehavior =
                FunctionChoiceBehavior.Auto(),

            Temperature = request.temperature ?? 1.0,

            TopP = request.topP ?? 0.95,

            MaxTokens = request.maxTokens ?? 1500,

            FrequencyPenalty = 0.2,

            PresencePenalty = 0.1
        };

    var fullResponse = "";

    await foreach (var chunk in
        chatService.GetStreamingChatMessageContentsAsync(
            history,
            executionSettings: settings,
            kernel: kernel
        ))
    {
        if (chunk.Content != null)
        {
            fullResponse += chunk.Content;
        }
    }
Console.WriteLine($"LLM RESPONSE GENERATED: {fullResponse.Length} chars");

    history.AddAssistantMessage(
        fullResponse
    );
    string insertSql =
        @"INSERT INTO QuestionCache
        (
            Question,
            QuestionEmbedding,
            Answer,
            DocumentId
        )
        VALUES
        (
            @Question,
            @Embedding,
            @Answer,
            @DocumentId
        )";

    if (!fullResponse.Contains("Insufficient supporting evidence"))
    {
        using SqlCommand insertCommand =
            new SqlCommand(
                insertSql,
                connection
            );

        insertCommand.Parameters.AddWithValue(
            "@Question",
            request.message
        );

        insertCommand.Parameters.AddWithValue(
            "@Embedding",
            embeddingString
        );

        insertCommand.Parameters.AddWithValue(
            "@Answer",
            fullResponse
        );

        insertCommand.Parameters.AddWithValue(
            "@DocumentId",
            (object)request.documentId ?? DBNull.Value
        );

        insertCommand.ExecuteNonQuery();

        Console.WriteLine(
            "SAVED TO CACHE"
        );
    }

    return Results.Ok(new
    {
        result = fullResponse,
        sources = sources,
        chunksRetrieved = relevantChunks.Count,
        similarityScore = confidenceScore / 100.0
    });
    }
    catch (Exception ex)
    {
        Console.WriteLine("================================");
        Console.WriteLine("CHAT ERROR");
        Console.WriteLine(ex.ToString());
        Console.WriteLine("================================");

        return Results.Problem(
            detail: ex.ToString(),
            title: "Chat Endpoint Error",
            statusCode: 500
        );
    }
});

app.MapPost("/upload",
async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var file = form.Files[0];
    
    string versionAction = form.ContainsKey("versionAction") ? form["versionAction"].ToString() : null;
    string targetGroupIdStr = form.ContainsKey("targetGroupId") ? form["targetGroupId"].ToString() : null;
    Guid? targetGroupId = !string.IsNullOrEmpty(targetGroupIdStr) ? Guid.Parse(targetGroupIdStr) : null;

    var uploadsFolder = "Uploads";
    Directory.CreateDirectory(uploadsFolder);
    var filePath = Path.Combine(uploadsFolder, file.FileName);

    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);
    byte[] fileBytes = memoryStream.ToArray();
    
    string fileHash = BitConverter.ToString(SHA256.HashData(fileBytes)).Replace("-", "").ToLowerInvariant();

    using SqlConnection connection = new SqlConnection(connectionString);
    connection.Open();

    string checkExactSql = "SELECT Id, VersionGroupId, Version, FileName FROM Documents WHERE FileHash = @FileHash AND Status = 'Latest'";
    using SqlCommand checkExactCmd = new SqlCommand(checkExactSql, connection);
    checkExactCmd.Parameters.AddWithValue("@FileHash", fileHash);
    using (var exactReader = checkExactCmd.ExecuteReader())
    {
        if (exactReader.Read())
        {
            return Results.Ok(new {
                status = "exact_duplicate",
                documentId = exactReader.GetInt32(0),
                versionGroupId = exactReader.GetGuid(1),
                version = exactReader.GetInt32(2),
                fileName = exactReader.GetString(3)
            });
        }
    }

    await File.WriteAllBytesAsync(filePath, fileBytes);

    var documentChunksWithPages = new List<(string Text, int PageNumber)>();
    string text = "";
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    try
    {
        if (extension == ".pdf")
        {
            using (var pdf = PdfDocument.Open(filePath))
            {
                int pageNum = 1;
                foreach (var page in pdf.GetPages())
                {
                    text += page.Text + "\n";
                    var pageChunks = ChunkText(page.Text);
                    foreach (var c in pageChunks)
                    {
                        documentChunksWithPages.Add((c, pageNum));
                    }
                    pageNum++;
                }
            }
        }
        else if (extension == ".txt" || extension == ".md" || extension == ".csv")
        {
            text = await File.ReadAllTextAsync(filePath);
            var chunks = ChunkText(text);
            foreach (var c in chunks) documentChunksWithPages.Add((c, 1));
        }
        else if (extension == ".docx")
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                text = wordDoc.MainDocumentPart?.Document.Body?.InnerText ?? "";
                var chunks = ChunkText(text);
                foreach (var c in chunks) documentChunksWithPages.Add((c, 1));
            }
        }
        else
        {
            return Results.BadRequest(new { error = "Unsupported file type" });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing file: {ex.Message}");
        return Results.BadRequest(new { error = "Failed to parse document" });
    }

    string summaryText = text.Length > 4000 ? text.Substring(0, 4000) : text;
    var summaryEmbeddings = await embeddingService.GenerateAsync(new[] { summaryText });
    var summaryEmbedding = summaryEmbeddings.First();
    float[] summaryEmbeddingVector = summaryEmbedding.Vector.ToArray();
    string summaryEmbeddingString = string.Join(",", summaryEmbeddingVector);

    if (string.IsNullOrEmpty(versionAction))
    {
        string similarSql = "SELECT Id, VersionGroupId, Version, FileName, SummaryEmbedding FROM Documents WHERE Status = 'Latest' AND SummaryEmbedding IS NOT NULL";
        using SqlCommand similarCmd = new SqlCommand(similarSql, connection);
        using SqlDataReader similarReader = similarCmd.ExecuteReader();
        
        int similarId = 0;
        Guid similarGroupId = Guid.Empty;
        int similarVersion = 0;
        string similarName = "";
        double maxSimilarity = 0;

        while (similarReader.Read())
        {
            try {
                float[] storedSummary = similarReader.GetString(4).Split(',').Select(float.Parse).ToArray();
                double sim = CosineSimilarity(summaryEmbeddingVector, storedSummary);
                if (sim > maxSimilarity)
                {
                    maxSimilarity = sim;
                    similarId = similarReader.GetInt32(0);
                    similarGroupId = similarReader.GetGuid(1);
                    similarVersion = similarReader.GetInt32(2);
                    similarName = similarReader.GetString(3);
                }
            } catch { continue; }
        }
        similarReader.Close();

        if (maxSimilarity > 0.95)
        {
            return Results.Ok(new {
                status = "similar_found",
                similarDocument = new {
                    id = similarId,
                    versionGroupId = similarGroupId,
                    version = similarVersion,
                    fileName = similarName,
                    similarity = maxSimilarity
                }
            });
        }
    }

    Guid finalGroupId = Guid.NewGuid();
    int finalVersion = 1;

    if (versionAction == "create_version" && targetGroupId.HasValue)
    {
        finalGroupId = targetGroupId.Value;
        
        using SqlCommand getMaxCmd = new SqlCommand("SELECT ISNULL(MAX(Version), 0) FROM Documents WHERE VersionGroupId = @GroupId", connection);
        getMaxCmd.Parameters.AddWithValue("@GroupId", finalGroupId);
        finalVersion = (int)getMaxCmd.ExecuteScalar() + 1;

        using SqlCommand archiveCmd = new SqlCommand("UPDATE Documents SET Status = 'Archived' WHERE VersionGroupId = @GroupId", connection);
        archiveCmd.Parameters.AddWithValue("@GroupId", finalGroupId);
        archiveCmd.ExecuteNonQuery();
    }
    else if (versionAction == "replace" && targetGroupId.HasValue)
    {
        finalGroupId = targetGroupId.Value;
        using SqlCommand getVersionCmd = new SqlCommand("SELECT TOP 1 Version FROM Documents WHERE VersionGroupId = @GroupId AND Status = 'Latest'", connection);
        getVersionCmd.Parameters.AddWithValue("@GroupId", finalGroupId);
        var vResult = getVersionCmd.ExecuteScalar();
        finalVersion = vResult != null ? (int)vResult : 1;

        using SqlCommand deleteCmd = new SqlCommand("DELETE FROM Documents WHERE VersionGroupId = @GroupId AND Status = 'Latest'", connection);
        deleteCmd.Parameters.AddWithValue("@GroupId", finalGroupId);
        deleteCmd.ExecuteNonQuery();
    }
    else if (versionAction == "store_separate")
    {
        finalGroupId = Guid.NewGuid();
        finalVersion = 1;
    }

    string documentSql = @"INSERT INTO Documents (FileName, FileHash, VersionGroupId, Version, Status, SummaryEmbedding) OUTPUT INSERTED.Id VALUES (@FileName, @FileHash, @VersionGroupId, @Version, 'Latest', @SummaryEmbedding)";
    using SqlCommand documentCommand = new SqlCommand(documentSql, connection);
    documentCommand.Parameters.AddWithValue("@FileName", file.FileName);
    documentCommand.Parameters.AddWithValue("@FileHash", fileHash);
    documentCommand.Parameters.AddWithValue("@VersionGroupId", finalGroupId);
    documentCommand.Parameters.AddWithValue("@Version", finalVersion);
    documentCommand.Parameters.AddWithValue("@SummaryEmbedding", summaryEmbeddingString);

    int documentId = (int)documentCommand.ExecuteScalar();

    foreach (var chunkTuple in documentChunksWithPages)
    {
        string chunk = chunkTuple.Text;
        int pageNumber = chunkTuple.PageNumber;

        string chunkHash = BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(chunk))).Replace("-", "").ToLowerInvariant();
        
        using SqlCommand checkChunk = new SqlCommand("SELECT Id FROM Chunks WHERE ChunkHash = @ChunkHash", connection);
        checkChunk.Parameters.AddWithValue("@ChunkHash", chunkHash);
        var existingChunkId = checkChunk.ExecuteScalar();
        int chunkId;

        if (existingChunkId != null)
        {
            chunkId = (int)existingChunkId;
        }
        else
        {
            var chunkEmbeddings = await embeddingService.GenerateAsync(new[] { chunk });
            var chunkEmbedding = chunkEmbeddings.First();
            string embeddingString = string.Join(",", chunkEmbedding.Vector.ToArray());

            string chunkSql = @"INSERT INTO Chunks (ChunkHash, ChunkText, Embedding) OUTPUT INSERTED.Id VALUES (@ChunkHash, @ChunkText, @Embedding)";
            using SqlCommand chunkCommand = new SqlCommand(chunkSql, connection);
            chunkCommand.Parameters.AddWithValue("@ChunkHash", chunkHash);
            chunkCommand.Parameters.AddWithValue("@ChunkText", chunk);
            chunkCommand.Parameters.AddWithValue("@Embedding", embeddingString);
            chunkId = (int)chunkCommand.ExecuteScalar();
        }

        string mapSql = @"IF NOT EXISTS (SELECT 1 FROM DocumentChunkMapping WHERE DocumentId = @DocumentId AND ChunkId = @ChunkId AND PageNumber = @PageNumber) INSERT INTO DocumentChunkMapping (DocumentId, ChunkId, PageNumber) VALUES (@DocumentId, @ChunkId, @PageNumber)";
        using SqlCommand mapCommand = new SqlCommand(mapSql, connection);
        mapCommand.Parameters.AddWithValue("@DocumentId", documentId);
        mapCommand.Parameters.AddWithValue("@ChunkId", chunkId);
        mapCommand.Parameters.AddWithValue("@PageNumber", pageNumber);
        mapCommand.ExecuteNonQuery();
    }

    try
    {
        string kgPrompt = await File.ReadAllTextAsync("Prompts/KGPrompt.txt");
        var kgResult = await kernel.InvokePromptAsync(kgPrompt, new() { ["input"] = summaryText });
        var kgJson = kgResult.GetValue<string>()!;
        
        // Strip markdown backticks if returned
        if (kgJson.StartsWith("```json")) kgJson = kgJson.Substring(7, kgJson.Length - 10);
        else if (kgJson.StartsWith("```")) kgJson = kgJson.Substring(3, kgJson.Length - 6);
        
        using JsonDocument jsonDoc = JsonDocument.Parse(kgJson.Trim());
        var root = jsonDoc.RootElement;
        var entityIdMap = new Dictionary<string, int>();
        
        if (root.TryGetProperty("entities", out JsonElement entities))
        {
            foreach(var entity in entities.EnumerateArray())
            {
                string eName = entity.GetProperty("name").GetString()!;
                string eType = entity.GetProperty("type").GetString()!;
                
                string insertEntity = "INSERT INTO GraphEntities (Name, Type) OUTPUT INSERTED.Id VALUES (@Name, @Type)";
                using SqlCommand cmd = new SqlCommand(insertEntity, connection);
                cmd.Parameters.AddWithValue("@Name", eName);
                cmd.Parameters.AddWithValue("@Type", eType);
                int eid = (int)cmd.ExecuteScalar();
                entityIdMap[eName] = eid;
            }
        }
        
        if (root.TryGetProperty("relationships", out JsonElement relationships))
        {
            foreach(var rel in relationships.EnumerateArray())
            {
                string source = rel.GetProperty("source").GetString()!;
                string target = rel.GetProperty("target").GetString()!;
                string relation = rel.GetProperty("relation").GetString()!;
                
                if (entityIdMap.TryGetValue(source, out int sourceId) && entityIdMap.TryGetValue(target, out int targetId))
                {
                    string insertRel = "INSERT INTO GraphRelationships (SourceEntityId, TargetEntityId, RelationType, DocumentId) VALUES (@Source, @Target, @Rel, @Doc)";
                    using SqlCommand cmd = new SqlCommand(insertRel, connection);
                    cmd.Parameters.AddWithValue("@Source", sourceId);
                    cmd.Parameters.AddWithValue("@Target", targetId);
                    cmd.Parameters.AddWithValue("@Rel", relation);
                    cmd.Parameters.AddWithValue("@Doc", documentId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error generating Knowledge Graph: " + ex.Message);
    }

    return Results.Ok(new
    {
        status = "stored",
        DocumentId = documentId,
        File = file.FileName,
        Chunks = documentChunksWithPages.Count,
        versionGroupId = finalGroupId,
        version = finalVersion
    });
})
.DisableAntiforgery();

app.MapGet("/documents", async () =>
{
    using SqlConnection connection = new SqlConnection(connectionString);
    connection.Open();
    
    string sql = @"
        SELECT d.Id, d.FileName, d.Version, d.Status, COUNT(m.ChunkId) AS ChunkCount, d.VersionGroupId
        FROM Documents d
        LEFT JOIN DocumentChunkMapping m ON d.Id = m.DocumentId
        WHERE d.Status = 'Latest'
        GROUP BY d.Id, d.FileName, d.Version, d.Status, d.VersionGroupId
        ORDER BY d.Id DESC";
        
    using SqlCommand command = new SqlCommand(sql, connection);
    using SqlDataReader reader = command.ExecuteReader();
    
    var docs = new List<object>();
    while (reader.Read())
    {
        docs.Add(new
        {
            id = reader.GetInt32(0),
            fileName = reader.GetString(1),
            version = reader.GetInt32(2),
            status = reader.GetString(3),
            chunks = reader.GetInt32(4),
            versionGroupId = reader.GetGuid(5)
        });
    }
    return Results.Ok(docs);
});

app.MapGet("/",
() =>
{
    return "Backend Working";
});
app.Run();

//
// REQUEST MODEL
//
double CosineSimilarity(
    float[] vectorA,
    float[] vectorB
)
{
    double dotProduct = 0;
    double magnitudeA = 0;
    double magnitudeB = 0;

    for (int i = 0; i < vectorA.Length; i++)
    {
        dotProduct +=
            vectorA[i] * vectorB[i];

        magnitudeA +=
            vectorA[i] * vectorA[i];

        magnitudeB +=
            vectorB[i] * vectorB[i];
    }

    return dotProduct /
        (
            Math.Sqrt(magnitudeA)
            *
            Math.Sqrt(magnitudeB)
        );
    }
    (List<string> Chunks,List<SourceInfo> Sources, double ConfidenceScore) GetRelevantChunks(
        float[] questionEmbedding,
        SqlConnection connection,
        int? documentId
    )
    {
        Console.WriteLine($"ENTERING GetRelevantChunks. Embedding length: {questionEmbedding.Length}, DocumentId: {documentId}");
        string sql =
            @"SELECT
                c.ChunkText,
                c.Embedding,
                (SELECT TOP 1 d2.FileName FROM Documents d2 JOIN DocumentChunkMapping m2 ON d2.Id = m2.DocumentId WHERE m2.ChunkId = c.Id AND d2.Status = 'Latest') as FileName,
                (SELECT TOP 1 m2.PageNumber FROM Documents d2 JOIN DocumentChunkMapping m2 ON d2.Id = m2.DocumentId WHERE m2.ChunkId = c.Id AND d2.Status = 'Latest') as PageNumber
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

        using SqlCommand command =
            new SqlCommand(
                sql,
                connection
            );

        if (documentId.HasValue)
        {
            command.Parameters.AddWithValue(
                "@DocumentId",
                documentId.Value
            );
        }

        using SqlDataReader reader =
            command.ExecuteReader();

        var chunks =
            new List<(string Text,double Score)>();
        var sources =
            new List<SourceInfo>();

        while (reader.Read())
        {
            try
            {
                string chunkText =
                    reader.GetString(0);

                string embeddingText =
                    reader.GetString(1);

                string fileName =
                    reader.GetString(2);

                int pageNumber = reader.IsDBNull(3) ? 1 : reader.GetInt32(3);

                float[] chunkEmbedding =
                    embeddingText
                        .Split(',')
                        .Select(float.Parse)
                        .ToArray();

                if (chunkEmbedding.Length != questionEmbedding.Length)
                {
                    continue;
                }

                double similarity =
                    CosineSimilarity(
                        questionEmbedding,
                        chunkEmbedding
                    );

                string formattedChunk = $"[Source: {fileName} | Page: {pageNumber}]\n{chunkText}";

                chunks.Add(
                    (
                        formattedChunk,
                        similarity
                    )
                );
                sources.Add(
                    new SourceInfo(
                        fileName,
                        pageNumber
                    )
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading chunk: {ex.Message}");
                continue;
            }
        }

        var topChunks = chunks.OrderByDescending(x => x.Score).Take(5).ToList();
        double avgScore = topChunks.Any() ? topChunks.Average(x => x.Score) : 0;
        double confidence = avgScore * 100.0;

        return (
            topChunks.Select(x => x.Text).ToList(),
            sources
                .Distinct()
                .ToList(),
            confidence
        );
    }
    record ChatRequest(string message, int? documentId, double? temperature, int? maxTokens, double? topP);

    record SourceInfo(
    string FileName,
    int PageNumber
);

