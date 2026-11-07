using Almer.Domain;

namespace Almer.Application;

public sealed record ProfitabilitySummary(decimal NetSales, decimal CostOfGoodsSold, decimal GrossProfit, decimal GrossMargin, decimal OperatingExpenses, decimal OtherIncome, decimal OperatingResult, decimal CostMatchRate, int MissingCostLines, bool IsComplete);

public static class ProfitabilityCalculator
{
    public static ProfitabilitySummary Calculate(IEnumerable<SaleLine> saleLines, IEnumerable<FinancialEntry> financialEntries)
    {
        var lines = saleLines.ToArray(); var entries = financialEntries.Where(x => x.IsApproved).ToArray();
        var netSales = lines.Sum(x => x.NetAmount); var cogs = lines.Sum(x => x.Quantity * x.UnitCost); var grossProfit = netSales - cogs;
        var expenses = entries.Where(x => x.Type == FinancialEntryType.Expense).Sum(x => x.NetAmount); var income = entries.Where(x => x.Type == FinancialEntryType.Income).Sum(x => x.NetAmount);
        var matchRate = lines.Length == 0 ? 0m : decimal.Round(lines.Count(x => x.UnitCost > 0) * 100m / lines.Length, 1);
        return new(netSales, cogs, grossProfit, netSales == 0 ? 0 : decimal.Round(grossProfit / netSales * 100m, 2), expenses, income, grossProfit + income - expenses, matchRate, lines.Count(x => x.UnitCost <= 0), lines.Length > 0 && lines.All(x => x.UnitCost > 0));
    }
}
