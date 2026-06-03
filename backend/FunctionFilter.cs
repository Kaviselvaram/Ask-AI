using Microsoft.SemanticKernel;

public class FunctionFilter :
    IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        Console.WriteLine(
            $"Function Called: {context.Function.Name}");

        //
        // BLOCK DANGEROUS FUNCTION CALLS
        //

        if (context.Function.Name
            .Contains("Delete"))
        {
            throw new Exception(
                "Dangerous function blocked.");
        }

        //
        // CONTINUE EXECUTION
        //

        await next(context);

        Console.WriteLine(
            $"Function Finished: {context.Function.Name}");
    }
}