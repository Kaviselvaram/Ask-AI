using Microsoft.SemanticKernel;

public class StartupPlugin
{
    [KernelFunction]
    public string CalculateRevenue(
        int users,
        int price)
    {
        int revenue = users * price;

        return
            $"Estimated monthly revenue is ${revenue}";
    }

    [KernelFunction]
    public string EstimateValuation(
        int monthlyRevenue)
    {
        int valuation =
            monthlyRevenue * 12 * 5;

        return
            $"Estimated startup valuation is ${valuation}";
    }
}