using Almer.Application;
using Almer.Domain;

namespace Almer.Tests;
public sealed class FinancialCalculatorTests
{
    [Fact]
    public void Calculates_financial_formulas_with_decimal_precision()
    {
        var rows = new[] { new DailyMetric { GrossSales = 1190m, Vat = 190m, Returns = 50m, Discounts = 20m, CostOfGoodsSold = 600m, OperatingExpenses = 100m } };
        var result = FinancialCalculator.Calculate(rows);
        Assert.Equal(930m, result.NetSales);
        Assert.Equal(330m, result.GrossProfit);
        Assert.Equal(35.48m, result.Margin);
        Assert.Equal(230m, result.OperatingResult);
    }
}

