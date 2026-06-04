using System;
using System.IO;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.SemanticKernel;

namespace backend.Services
{
    public class IntentClassifier : IIntentClassifier
    {
        private readonly Kernel _kernel;

        public IntentClassifier(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<QueryIntent> ClassifyAsync(string query)
        {
            try
            {
                string promptPath = Path.Combine(Directory.GetCurrentDirectory(), "Prompts", "IntentPrompt.txt");
                string promptTemplate = await File.ReadAllTextAsync(promptPath);
                
                var arguments = new KernelArguments { { "input", query } };
                var result = await _kernel.InvokePromptAsync(promptTemplate, arguments);
                
                string resultText = result.GetValue<string>()?.Trim() ?? string.Empty;
                
                if (Enum.TryParse<QueryIntent>(resultText, true, out var intent))
                {
                    return intent;
                }
                
                return QueryIntent.Unknown;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntentClassifier] Error: {ex.Message}");
                return QueryIntent.Unknown;
            }
        }
    }
}
