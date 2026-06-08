using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests
{
    public class CustomRunner
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("==========================================================================");
            Console.WriteLine("                  ASK-AI FULL PLATFORM TEST RUNNER                        ");
            Console.WriteLine("==========================================================================");
            Console.WriteLine();

            int totalPassed = 0;
            int totalFailed = 0;

            var assembly = Assembly.GetExecutingAssembly();
            
            // Find all classes that have [Fact] methods
            var testClasses = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttributes(typeof(FactAttribute), false).Any()))
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var testClass in testClasses)
            {
                Console.WriteLine($"\n--- Running Test Module: {testClass.Name} ---");
                
                var testMethods = testClass.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(FactAttribute), false).Any())
                    .OrderBy(m => m.Name)
                    .ToList();

                foreach (var method in testMethods)
                {
                    Console.Write($"Executing {method.Name}... ");

                    try
                    {
                        var instance = Activator.CreateInstance(testClass);
                        if (instance is IAsyncLifetime asyncLifetime)
                        {
                            await asyncLifetime.InitializeAsync();
                        }

                        if (method.Invoke(instance, null) is Task task)
                        {
                            await task;
                        }

                        if (instance is IAsyncLifetime asyncLifetimeEnd)
                        {
                            await asyncLifetimeEnd.DisposeAsync();
                        }
                        if (instance is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[PASS]");
                        Console.ResetColor();
                        totalPassed++;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[FAIL]");
                        Console.ResetColor();
                        
                        // Extract inner exception which contains the actual Assert failure message
                        var actualError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        Console.WriteLine($"      -> {actualError.Replace("\n", "\n      -> ")}");
                        
                        totalFailed++;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("==========================================================================");
            Console.WriteLine("                             FINAL SUMMARY                                ");
            Console.WriteLine("==========================================================================");
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"    Total Passed: {totalPassed}");
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    Total Failed: {totalFailed}");
            
            Console.ResetColor();
            Console.WriteLine($"    Total Tests:  {totalPassed + totalFailed}");
            Console.WriteLine("==========================================================================");
        }
    }
}
