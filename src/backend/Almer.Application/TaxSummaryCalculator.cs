using Almer.Domain;

namespace Almer.Application;

public sealed record TaxBreakdown(decimal Rate, decimal SalesNet, decimal OutputTax, decimal PurchaseInputTax, decimal ExpenseInputTax, decimal NetTax);

public static class TaxSummaryCalculator
{
    public static IReadOnlyList<TaxBreakdown> Calculate(IEnumerable<SaleLine> sales, IEnumerable<PurchaseLine> purchases, IEnumerable<FinancialEntry> entries)
    {
        var saleRows = sales.ToArray(); var purchaseRows = purchases.ToArray(); var expenses = entries.Where(x => x.IsApproved && x.Type == FinancialEntryType.Expense).ToArray();
        return saleRows.Select(x => x.TaxRate).Concat(purchaseRows.Select(x => x.TaxRate)).Concat(expenses.Select(x => x.TaxRate)).Distinct().OrderBy(x => x).Select(rate =>
        {
            var salesNet = saleRows.Where(x => x.TaxRate == rate).Sum(x => x.NetAmount); var output = saleRows.Where(x => x.TaxRate == rate).Sum(x => x.TaxAmount); var purchase = purchaseRows.Where(x => x.TaxRate == rate).Sum(x => x.TaxAmount); var expense = expenses.Where(x => x.TaxRate == rate).Sum(x => x.TaxAmount);
            return new TaxBreakdown(rate, salesNet, output, purchase, expense, output - purchase - expense);
        }).ToArray();
    }
}
