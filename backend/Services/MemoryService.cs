using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel;

namespace backend.Services
{
    public class MemoryService : IMemoryService
    {
        private readonly string _connectionString;
        private readonly Kernel _kernel;

        public MemoryService(string connectionString, Kernel kernel)
        {
            _connectionString = connectionString;
            _kernel = kernel;
        }

        public async Task SaveMessageAsync(string conversationId, string role, string content)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                string sql = "INSERT INTO ConversationMemories (ConversationId, Role, Content) VALUES (@ConversationId, @Role, @Content)";
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ConversationId", conversationId);
                command.Parameters.AddWithValue("@Role", role);
                command.Parameters.AddWithValue("@Content", content);
                
                await command.ExecuteNonQueryAsync();
                Console.WriteLine("MEMORY SAVED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MEMORY FAILED (SaveMessageAsync): {ex.Message}");
            }
        }

        public async Task<List<ConversationMemory>> GetRecentMessagesAsync(string conversationId)
        {
            var messages = new List<ConversationMemory>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                // Get last 20 messages
                string sql = "SELECT Role, Content, Timestamp FROM ConversationMemories WHERE ConversationId = @ConversationId ORDER BY Timestamp DESC";
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ConversationId", conversationId);
                
                using var reader = await command.ExecuteReaderAsync();
                var tempMessages = new List<ConversationMemory>();
                
                int count = 0;
                while (await reader.ReadAsync() && count < 20)
                {
                    tempMessages.Add(new ConversationMemory(
                        conversationId,
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetDateTime(2)
                    ));
                    count++;
                }
                
                // Reverse to get chronological order
                tempMessages.Reverse();
                messages = tempMessages;
                Console.WriteLine("MEMORY RETRIEVED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MEMORY FAILED (GetRecentMessagesAsync): {ex.Message}");
            }
            
            return messages;
        }

        public async Task ExtractAndSaveEntitiesAsync(string conversationId, string query)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                string promptPath = Path.Combine(Directory.GetCurrentDirectory(), "Prompts", "EntityExtractionPrompt.txt");
                string promptTemplate = await File.ReadAllTextAsync(promptPath, cts.Token);
                
                var arguments = new KernelArguments { { "input", query } };
                var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, cancellationToken: cts.Token);
                string jsonOutput = result.GetValue<string>()?.Trim() ?? "";

                if (jsonOutput.StartsWith("```json"))
                {
                    jsonOutput = jsonOutput.Substring(7);
                    if (jsonOutput.EndsWith("```"))
                    {
                        jsonOutput = jsonOutput.Substring(0, jsonOutput.Length - 3);
                    }
                }
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entities = JsonSerializer.Deserialize<List<string>>(jsonOutput, options);
                
                if (entities != null && entities.Count > 0)
                {
                    Console.WriteLine($"ENTITY EXTRACTED: {string.Join(", ", entities)}");
                    
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    
                    foreach (var entity in entities)
                    {
                        string sql = @"
                            IF EXISTS (SELECT 1 FROM ConversationEntities WHERE ConversationId = @ConversationId AND EntityName = @EntityName)
                            BEGIN
                                UPDATE ConversationEntities SET LastReferenced = GETUTCDATE() WHERE ConversationId = @ConversationId AND EntityName = @EntityName;
                            END
                            ELSE
                            BEGIN
                                INSERT INTO ConversationEntities (ConversationId, EntityName) VALUES (@ConversationId, @EntityName);
                            END
                        ";
                        using var command = new SqlCommand(sql, connection);
                        command.Parameters.AddWithValue("@ConversationId", conversationId);
                        command.Parameters.AddWithValue("@EntityName", entity);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("MEMORY FAILED (ExtractAndSaveEntitiesAsync): Timeout exceeded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MEMORY FAILED (ExtractAndSaveEntitiesAsync): {ex.Message}");
            }
        }

        public async Task<List<ConversationEntity>> GetRecentEntitiesAsync(string conversationId)
        {
            var entities = new List<ConversationEntity>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                // Get entities referenced in the last 2 hours to avoid stale context
                string sql = "SELECT EntityName, LastReferenced FROM ConversationEntities WHERE ConversationId = @ConversationId AND LastReferenced > DATEADD(hour, -2, GETUTCDATE()) ORDER BY LastReferenced DESC";
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ConversationId", conversationId);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    entities.Add(new ConversationEntity(
                        conversationId,
                        reader.GetString(0),
                        reader.GetDateTime(1)
                    ));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MEMORY FAILED (GetRecentEntitiesAsync): {ex.Message}");
            }
            
            return entities;
        }
    }
}
