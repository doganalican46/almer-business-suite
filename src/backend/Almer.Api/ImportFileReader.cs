using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace Almer.Api;

public sealed record ImportOptions(string Delimiter = ";", string Encoding = "utf-8", string DecimalSeparator = ",", string DateFormat = "dd.MM.yyyy");
public sealed record ImportTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

public static class ImportFileReader
{
    private const int MaxRows = 50_000;

    public static ImportTable Read(byte[] data, string fileName, ImportOptions options)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ReadCsv(data, options),
            ".xlsx" or ".xls" => ReadExcel(data),
            _ => throw new ArgumentException("Only XLSX, XLS and CSV files are supported.")
        };
    }

    private static ImportTable ReadExcel(byte[] data)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new MemoryStream(data);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var raw = new List<string[]>();
        do
        {
            while (reader.Read())
            {
                var cells = Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim() ?? "").ToArray();
                if (cells.Any(x => x.Length > 0)) raw.Add(cells);
                if (raw.Count > MaxRows + 1) throw new ArgumentException($"Files may contain at most {MaxRows:N0} rows.");
            }
        } while (raw.Count == 0 && reader.NextResult());
        return ToTable(raw);
    }

    private static ImportTable ReadCsv(byte[] data, ImportOptions options)
    {
        if (options.Delimiter.Length != 1 || !new[] { ',', ';', '\t', '|' }.Contains(options.Delimiter[0])) throw new ArgumentException("Unsupported CSV delimiter.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = options.Encoding.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => new UTF8Encoding(false, true),
            "windows-1252" or "cp1252" => Encoding.GetEncoding(1252),
            "iso-8859-9" => Encoding.GetEncoding("iso-8859-9"),
            _ => throw new ArgumentException("Unsupported CSV encoding.")
        };
        var text = encoding.GetString(data).TrimStart('\uFEFF');
        var rows = ParseCsv(text, options.Delimiter[0]);
        if (rows.Count > MaxRows + 1) throw new ArgumentException($"Files may contain at most {MaxRows:N0} rows.");
        return ToTable(rows);
    }

    private static List<string[]> ParseCsv(string text, char delimiter)
    {
        var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == delimiter && !quoted) { row.Add(cell.ToString().Trim()); cell.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString().Trim()); cell.Clear();
                if (row.Any(x => x.Length > 0)) rows.Add(row.ToArray()); row = [];
            }
            else cell.Append(c);
        }
        row.Add(cell.ToString().Trim()); if (row.Any(x => x.Length > 0)) rows.Add(row.ToArray());
        if (quoted) throw new ArgumentException("CSV contains an unterminated quoted field.");
        return rows;
    }

    private static ImportTable ToTable(List<string[]> raw)
    {
        if (raw.Count < 2) throw new ArgumentException("The file must contain a header and at least one data row.");
        var columns = raw[0].Select((x, i) => string.IsNullOrWhiteSpace(x) ? $"Column{i + 1}" : x.Trim()).ToArray();
        if (columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length) throw new ArgumentException("Column names must be unique.");
        var rows = raw.Skip(1).Select(values => (IReadOnlyDictionary<string, string>)columns.Select((column, index) => new { column, value = index < values.Length ? values[index] : "" }).ToDictionary(x => x.column, x => x.value, StringComparer.OrdinalIgnoreCase)).ToArray();
        return new ImportTable(columns, rows);
    }

    public static bool TryDecimal(string value, ImportOptions options, out decimal number)
    {
        var separator = options.DecimalSeparator == "." ? "." : ",";
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = separator; culture.NumberFormat.NumberGroupSeparator = separator == "," ? "." : ",";
        return decimal.TryParse(value, NumberStyles.Number, culture, out number);
    }

    public static bool TryDate(string value, ImportOptions options, out DateTimeOffset date)
    {
        if (DateTimeOffset.TryParseExact(value, options.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date)) return true;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);
    }
}
