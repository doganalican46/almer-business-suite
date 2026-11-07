using System.Text;
using Almer.Api;

namespace Almer.Tests;

public sealed class ImportCenterTests
{
    [Fact]
    public void Csv_reader_supports_quoted_delimiters_and_utf8()
    {
        var csv = "sku;nameDe;nameTr\r\nALM-100;\"Öl, nativ\";\"Zeytinyağı\"\r\n";
        var table = ImportFileReader.Read(Encoding.UTF8.GetBytes(csv), "products.csv", new());
        Assert.Single(table.Rows);
        Assert.Equal("Öl, nativ", table.Rows[0]["nameDe"]);
        Assert.Equal("Zeytinyağı", table.Rows[0]["nameTr"]);
    }

    [Theory]
    [InlineData("1.234,56", ",", 1234.56)]
    [InlineData("1,234.56", ".", 1234.56)]
    public void Decimal_parser_supports_german_and_international_formats(string input, string separator, decimal expected)
    {
        Assert.True(ImportFileReader.TryDecimal(input, new ImportOptions(DecimalSeparator: separator), out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Csv_reader_rejects_unterminated_quotes()
    {
        var csv = "sku;name\nALM-1;\"broken";
        Assert.Throws<ArgumentException>(() => ImportFileReader.Read(Encoding.UTF8.GetBytes(csv), "bad.csv", new()));
    }
}
