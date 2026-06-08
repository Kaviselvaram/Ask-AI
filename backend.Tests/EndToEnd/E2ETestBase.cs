using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd
{
    public abstract class E2ETestBase : IAsyncLifetime
    {
        protected readonly HttpClient Client;
        protected static bool IsSeeded = false;
        private static readonly System.Threading.SemaphoreSlim _seedLock = new System.Threading.SemaphoreSlim(1, 1);

        public E2ETestBase()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            Client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5213"),
                Timeout = TimeSpan.FromMinutes(5) // End-to-end LLM calls take time
            };
        }

        public async Task InitializeAsync()
        {
            if (!IsSeeded)
            {
                await _seedLock.WaitAsync();
                try
                {
                    if (!IsSeeded)
                    {
                        await SeedDatabaseAsync();
                        IsSeeded = true;
                    }
                }
                finally
                {
                    _seedLock.Release();
                }
            }
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        private async Task SeedDatabaseAsync()
        {
            var existingDocs = "";
            var docResponse = await Client.GetAsync("/documents");
            if (docResponse.IsSuccessStatusCode)
            {
                existingDocs = await docResponse.Content.ReadAsStringAsync();
            }

            // Seed AIOS document
            if (!existingDocs.Contains("AIOS_Revised_Product_Blueprint"))
            {
                string aiosContent = "AIOS is an advanced artificial intelligence operating system designed for schools. It automates administrative workflows, analyzes student performance data, and provides predictive insights. Risks include data integration challenges and privacy concerns.";
                await UploadDocumentAsync("AIOS_Revised_Product_Blueprint.txt", aiosContent);
            }

            // Seed Bookreview document
            if (!existingDocs.Contains("Bookreview report"))
            {
                string bookReviewContent = "This is a bookreview report. The book being reviewed is Deep Work by Cal Newport. It discusses the ability to focus without distraction on a cognitively demanding task.";
                await UploadDocumentAsync("Bookreview report.txt", bookReviewContent);
            }
            
            // Give the embeddings a moment to process (though they should be synchronous in our backend)
            await Task.Delay(2000);
        }

        private async Task UploadDocumentAsync(string fileName, string content)
        {
            using var contentStream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(content));
            using var requestContent = new MultipartFormDataContent();
            using var streamContent = new StreamContent(contentStream);
            
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            requestContent.Add(streamContent, "file", fileName);

            var response = await Client.PostAsync("/upload", requestContent);
            response.EnsureSuccessStatusCode();
        }

        protected async Task<string> ChatAsync(string message, string conversationId = null)
        {
            var payload = new
            {
                message = message,
                conversationId = conversationId
            };
            
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await Client.PostAsync("/chat", content);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
    }
}
