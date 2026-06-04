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

builder.Services.AddScoped<IIntentClassifier, IntentClassifier>();
builder.Services.AddScoped<IPlannerService, PlannerService>();
builder.Services.AddScoped<IMemoryService>(sp => {
    string connStr = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")! + ";Pooling=false";
    var kernel = sp.GetRequiredService<Kernel>();
    return new MemoryService(connStr, kernel);
});
builder.Services.AddSingleton<RetrievalStrategyFactory>();

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

Console.WriteLine("Connected to Azure SQL!");
Console.WriteLine("SQL Connection String Loaded");

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
builder.Services.AddSingleton(kernel);

var embeddingService =
    kernel.Services.GetRequiredService<
        Microsoft.Extensions.AI.IEmbeddingGenerator<
            string,
            Microsoft.Extensions.AI.Embedding<float>
        >
    >();
kernel.Plugins.AddFromType<StartupPlugin>();

var app = builder.Build();

// app.UseAntiforgery();


app.UseSwagger();

app.UseSwaggerUI();

app.UseCors(policy =>
    policy.AllowAnyOrigin()
          .AllowAnyHeader()
          .AllowAnyMethod());


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
async (ChatRequest request, IIntentClassifier intentClassifier, RetrievalStrategyFactory strategyFactory, IPlannerService plannerService, IMemoryService memoryService) =>
{
    var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();
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

    // Feature 5: Query Rewriter + Memory Injection
    string isMemoryEnabledStr = Environment.GetEnvironmentVariable("Memory:Enabled") ?? "true";
    bool isMemoryEnabled = bool.TryParse(isMemoryEnabledStr, out bool parsed) ? parsed : true;

    string conversationHistory = "";
    string recentEntities = "";

    if (isMemoryEnabled && !string.IsNullOrEmpty(request.conversationId))
    {
        // Fire and forget entity extraction
        _ = memoryService.ExtractAndSaveEntitiesAsync(request.conversationId, request.message);
        
        // Save user message
        await memoryService.SaveMessageAsync(request.conversationId, "User", request.message);

        var messages = await memoryService.GetRecentMessagesAsync(request.conversationId);
        var entities = await memoryService.GetRecentEntitiesAsync(request.conversationId);

        if (messages.Count > 0)
        {
            conversationHistory = "Previous Messages:\n" + string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));
        }
        if (entities.Count > 0)
        {
            recentEntities = "Recent Entities:\n" + string.Join(", ", entities.Select(e => e.EntityName));
        }
    }

    string rewritePrompt = await File.ReadAllTextAsync("Prompts/RewritePrompt.txt");
    Console.WriteLine("RewritePrompt LOADED");  
    Console.WriteLine("ABOUT TO CALL KERNEL");
    var rewriteResult = await kernel.InvokePromptAsync(rewritePrompt, new() { 
        ["input"] = request.message,
        ["history"] = conversationHistory,
        ["entities"] = recentEntities
    });
    Console.WriteLine("KERNEL CALL SUCCESS");
    string rewrittenQuery = rewriteResult.GetValue<string>()?.Trim() ?? request.message;
    Console.WriteLine($"Original Query: {request.message} | Rewritten: {rewrittenQuery}");

    var intentStopwatch = System.Diagnostics.Stopwatch.StartNew();
    var intent = await intentClassifier.ClassifyAsync(rewrittenQuery);
    intentStopwatch.Stop();

    var strategy = strategyFactory.GetStrategy(intent);
    
    Console.WriteLine($"Intent: {intent}");
    Console.WriteLine($"Strategy TopChunks: {strategy.TopChunks}, UseVectorSearch: {strategy.UseVectorSearch}");
    Console.WriteLine($"Classification Latency: {intentStopwatch.ElapsedMilliseconds} ms");

    var plannerStopwatch = System.Diagnostics.Stopwatch.StartNew();
    var plan = await plannerService.CreatePlanAsync(rewrittenQuery, intent);
    plannerStopwatch.Stop();
    
    Console.WriteLine($"PLAN STRATEGY: {plan.Strategy}");
    Console.WriteLine($"PLAN STEPS: {string.Join(", ", plan.Steps)}");
    Console.WriteLine($"Planning Latency: {plannerStopwatch.ElapsedMilliseconds} ms");
    
    // Override default intent strategy chunk count with planner recommendation
    int topChunks = plan.RecommendedChunkCount;
    bool useVectorSearch = strategy.UseVectorSearch;
    if (plan.Strategy == "MetadataLookup") useVectorSearch = false;

    // NEW: CLASSIFICATION ROUTING
    var classification = await classifier.ClassifyTaskAsync(rewrittenQuery);
    if (classification.TaskType != "SimpleRetrieval" && classification.Confidence > 60 && plan.Strategy != "MetadataLookup")
    {
        var agentState = await orchestrator.ExecuteAsync(request.message, request.documentId, conversationHistory, recentEntities);
        
        if (isMemoryEnabled && !string.IsNullOrEmpty(request.conversationId))
        {
            await memoryService.SaveMessageAsync(request.conversationId, "Assistant", agentState.FinalReport);
        }

        return Results.Ok(new {
            result = agentState.FinalReport,
            sources = agentState.Sources.Distinct().ToList(),
            chunksRetrieved = agentState.Evidence.Count,
            similarityScore = agentState.GlobalConfidenceScore / 100.0
        });
    }

var retrievalStopwatch = System.Diagnostics.Stopwatch.StartNew();
List<string> relevantChunks = new List<string>();
List<SourceInfo> sources = new List<SourceInfo>();
double confidenceScore = 100.0;
float[] currentEmbedding = new float[1536];

if (useVectorSearch)
{
    Console.WriteLine("ABOUT TO GENERATE EMBEDDINGS");
    var embeddings = await embeddingService.GenerateAsync(new[] { rewrittenQuery });
    Console.WriteLine("EMBEDDING SUCCESS");
    
    var embedding = embeddings.First();
    currentEmbedding = embedding.Vector.ToArray();
    
    Console.WriteLine("ABOUT TO RETRIEVE RELEVANT CHUNKS");
    var result = GetRelevantChunks(rewrittenQuery, currentEmbedding, connection, request.documentId, topChunks, plan.RequiresMultiDocumentReasoning);
    relevantChunks = result.Chunks;
    sources = result.Sources;
    confidenceScore = result.ConfidenceScore;
    Console.WriteLine($"RETRIEVED {relevantChunks.Count} RELEVANT CHUNKS WITH CONFIDENCE {confidenceScore}");
}
else
{
    Console.WriteLine("BYPASSING VECTOR SEARCH - QUERYING METADATA");
    string sysSql = "SELECT Id, FileName FROM Documents WHERE Status = 'Latest'";
    if (request.documentId.HasValue) sysSql += " AND Id = @DocId";
    
    using SqlCommand sysCmd = new SqlCommand(sysSql, connection);
    if (request.documentId.HasValue) sysCmd.Parameters.AddWithValue("@DocId", request.documentId.Value);
    
    using SqlDataReader sysReader = await sysCmd.ExecuteReaderAsync();
    var fileNames = new List<string>();
    while (await sysReader.ReadAsync())
    {
        int docId = sysReader.GetInt32(0);
        string fName = sysReader.GetString(1);
        fileNames.Add(fName);
        sources.Add(new SourceInfo { ReferenceId = sources.Count + 1, DocumentId = docId, FileName = fName, DownloadUrl = $"/download/{docId}" });
    }
    relevantChunks.Add($"System Metadata:\nThe following files are available in the system:\n" + string.Join("\n", fileNames));
}
retrievalStopwatch.Stop();
Console.WriteLine($"Retrieval Latency: {retrievalStopwatch.ElapsedMilliseconds} ms");

// Removed hard short-circuit; we now trust the LLM to refuse based on system prompt.

    // Feature 3: Knowledge Graph Retrieval
    string kgContext = "";
    if (plan.RequiresKnowledgeGraph)
    {
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
    }

    string confidenceWarning = "";
    if (confidenceScore < 40 && relevantChunks.Any())
    {
        confidenceWarning = $"\n\nConfidence: Low. Verification recommended.";
    }
    else if (confidenceScore >= 40 && confidenceScore <= 60)
    {
        confidenceWarning = $"\n\nConfidence: Medium";
    }

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

[CONVERSATION CONTEXT]
{conversationHistory}
{recentEntities}

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

    if (isMemoryEnabled && !string.IsNullOrEmpty(request.conversationId))
    {
        await memoryService.SaveMessageAsync(request.conversationId, "Assistant", fullResponse);
    }
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

    overallStopwatch.Stop();
    Console.WriteLine($"Total Request Latency: {overallStopwatch.ElapsedMilliseconds} ms");

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

    var uploadsFolder = Path.Combine(app.Environment.ContentRootPath, "Uploads");
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

app.MapGet("/download/{documentId:int}", async (int documentId, HttpContext context) =>
{
    using SqlConnection connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    
    string sql = "SELECT FileName FROM Documents WHERE Id = @Id";
    using SqlCommand cmd = new SqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@Id", documentId);
    
    var fileNameObj = await cmd.ExecuteScalarAsync();
    if (fileNameObj == null) return Results.NotFound(new { error = "Document not found." });
    
    string fileName = fileNameObj.ToString();
    string filePath = Path.Combine(app.Environment.ContentRootPath, "Uploads", fileName);
    
    if (!System.IO.File.Exists(filePath)) 
    {
        Console.WriteLine($"[DOWNLOAD ERROR] File not found on disk.");
        Console.WriteLine($"DocumentId: {documentId}");
        Console.WriteLine($"FileName: {fileName}");
        Console.WriteLine($"Expected Path: {filePath}");
        Console.WriteLine($"Exists: false");
        return Results.NotFound(new { error = "File not found on disk.", path = filePath, fileName = fileName });
    }
    
    var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    if (!provider.TryGetContentType(fileName, out var contentType))
    {
        contentType = "application/octet-stream";
    }
    
    return Results.File(Path.GetFullPath(filePath), contentType, fileName);
});

app.MapGet("/documents/health", async () =>
{
    using SqlConnection connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    
    string sql = "SELECT Id, FileName FROM Documents WHERE Status = 'Latest'";
    using SqlCommand cmd = new SqlCommand(sql, connection);
    using var reader = await cmd.ExecuteReaderAsync();
    
    var results = new List<object>();
    var uploadsFolder = Path.Combine(app.Environment.ContentRootPath, "Uploads");

    while (await reader.ReadAsync())
    {
        int documentId = reader.GetInt32(0);
        string fileName = reader.GetString(1);
        string filePath = Path.Combine(uploadsFolder, fileName);
        bool exists = System.IO.File.Exists(filePath);
        
        results.Add(new {
            documentId = documentId,
            fileName = fileName,
            existsOnDisk = exists
        });
    }
    
    return Results.Ok(results);
});

app.MapGet("/intent/health", () =>
{
    return Results.Ok(new { status = "healthy" });
});

app.MapGet("/planner/health", () =>
{
    return Results.Ok(new { status = "healthy" });
});

app.MapGet("/memory/health", () =>
{
    return Results.Ok(new { status = "healthy" });
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
        string query,
        float[] questionEmbedding,
        SqlConnection connection,
        int? documentId,
        int topChunks,
        bool requiresMultiDocumentReasoning
    )
    {
        Console.WriteLine($"ENTERING GetRelevantChunks. Embedding length: {questionEmbedding.Length}, DocumentId: {documentId}");
        string sql =
            @"SELECT
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

        var queryWords = query.ToLowerInvariant().Split(new[] { ' ', '?', '.', ',' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();
        var chunksData = new List<(string Text, double Score, int DocId, string FileName, int PageNumber)>();

        while (reader.Read())
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
                {
                    continue;
                }

                double similarity = CosineSimilarity(questionEmbedding, chunkEmbedding);

                if (queryWords.Any(w => fileName.ToLowerInvariant().Contains(w)))
                {
                    similarity += 0.05;
                }

                Console.WriteLine($"Chunk Similarity: {similarity:F4} | Doc: {fileName}");
                chunksData.Add((chunkText, similarity, docId, fileName, pageNumber));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading chunk: {ex.Message}");
                continue;
            }
        }

        var filteredChunks = chunksData.Where(x => x.Score > 0.25).OrderByDescending(x => x.Score).Take(topChunks).ToList();
        
        var sources = new List<SourceInfo>();
        var finalChunks = new List<string>();
        int totalPageRefs = 0;

        foreach (var chunk in filteredChunks)
        {
            if (chunk.DocId == 0 || chunk.FileName == "Unknown") continue;

            var existingSource = sources.FirstOrDefault(s => s.DocumentId == chunk.DocId);
            int currentRefId;

            if (existingSource == null)
            {
                int maxDocs = requiresMultiDocumentReasoning ? 10 : 1;
                if (sources.Count >= maxDocs) continue; 
                
                currentRefId = sources.Count + 1;
                existingSource = new SourceInfo
                {
                    ReferenceId = currentRefId,
                    DocumentId = chunk.DocId,
                    FileName = chunk.FileName,
                    DownloadUrl = $"/download/{chunk.DocId}"
                };
                sources.Add(existingSource);
            }
            else
            {
                currentRefId = existingSource.ReferenceId;
            }

            if (totalPageRefs < 5 && !existingSource.Pages.Contains(chunk.PageNumber))
            {
                existingSource.Pages.Add(chunk.PageNumber);
                totalPageRefs++;
            }

            string formattedChunk = $"[Source {currentRefId}: {chunk.FileName} | Page: {chunk.PageNumber}]\n{chunk.Text}";
            finalChunks.Add(formattedChunk);
        }

        double avgScore = filteredChunks.Any() ? filteredChunks.Average(x => x.Score) : 0;
        double confidence = avgScore * 100.0;

        Console.WriteLine($"Top Chunk Score: {(filteredChunks.Any() ? filteredChunks.First().Score.ToString("F4") : "N/A")}");
        Console.WriteLine($"Confidence: {confidence:F1}%");
        Console.WriteLine($"Chunks Retrieved: {finalChunks.Count}");

        return (
            finalChunks,
            sources,
            confidence
        );
    }
    record ChatRequest(string message, int? documentId, double? temperature, int? maxTokens, double? topP, string? conversationId);
