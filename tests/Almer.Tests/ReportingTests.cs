using System.Text;
using Almer.Api;
using Almer.Application;
using Almer.Domain;
using ClosedXML.Excel;

namespace Almer.Tests;

public sealed class ReportingTests
{
    private static readonly ReportDataset Dataset = new("Umsatz & Kârlılık", "2026-10-01 - 2026-10-31", ["Produkt", "Wert"], [new[] { "Öl / Zeytinyağı", "12,50" }, new[] { "Formula", "=2+2" }]);

    [Fact]
    public void Csv_export_is_utf8_and_blocks_formula_injection()
    {
        var text = Encoding.UTF8.GetString(ReportExporter.ToCsv(Dataset));
        Assert.StartsWith("\uFEFF", text); Assert.Contains("Öl / Zeytinyağı", text); Assert.Contains("'=2+2", text);
    }

    [Fact]
    public void Excel_export_is_readable_and_blocks_formula_injection()
    {
        using var stream = new MemoryStream(ReportExporter.ToExcel(Dataset)); using var workbook = new XLWorkbook(stream);
        Assert.Equal("Umsatz & Kârlılık", workbook.Worksheet(1).Cell(1, 1).GetString()); Assert.False(workbook.Worksheet(1).Cell(6, 2).HasFormula); Assert.Equal("=2+2", workbook.Worksheet(1).Cell(6, 2).GetString());
    }

    [Fact]
    public void Pdf_export_is_a_readable_landscape_document()
    {
        var bytes = ReportExporter.ToPdf(Dataset); Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        var qaOutput = Environment.GetEnvironmentVariable("ALMER_PDF_QA_OUTPUT"); if (!string.IsNullOrWhiteSpace(qaOutput)) { Directory.CreateDirectory(Path.GetDirectoryName(qaOutput)!); File.WriteAllBytes(qaOutput, bytes); }
        Assert.True(bytes.Length > 1_000);
    }

    [Fact]
    public void Tax_summary_uses_returns_and_only_approved_expenses()
    {
        var sales = new[] { new SaleLine { TaxRate = 19, NetAmount = 100, TaxAmount = 19 }, new SaleLine { TaxRate = 19, NetAmount = -20, TaxAmount = -3.8m } };
        var purchases = new[] { new PurchaseLine { TaxRate = 19, TaxAmount = 5 } };
        var entries = new[] { new FinancialEntry { Type = FinancialEntryType.Expense, TaxRate = 19, TaxAmount = 2, IsApproved = true }, new FinancialEntry { Type = FinancialEntryType.Expense, TaxRate = 19, TaxAmount = 99, IsApproved = false } };
        var row = Assert.Single(TaxSummaryCalculator.Calculate(sales, purchases, entries)); Assert.Equal(80, row.SalesNet); Assert.Equal(15.2m, row.OutputTax); Assert.Equal(8.2m, row.NetTax);
    }
}
