using System.Text;
using ClosedXML.Excel;
using SkiaSharp;

namespace Almer.Api;

public sealed record ReportDataset(string Title, string Subtitle, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record ExportedReport(byte[] Content, string ContentType, string Extension);

public static class ReportExporter
{
    public static ExportedReport Export(ReportDataset data, string format) => format.ToLowerInvariant() switch
    {
        "csv" => new(ToCsv(data), "text/csv; charset=utf-8", "csv"),
        "xlsx" => new(ToExcel(data), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
        "pdf" => new(ToPdf(data), "application/pdf", "pdf"),
        _ => throw new ArgumentException("Supported export formats are CSV, XLSX and PDF.")
    };

    public static byte[] ToCsv(ReportDataset data)
    {
        var builder = new StringBuilder(); builder.Append('\uFEFF'); builder.AppendLine(string.Join(';', data.Headers.Select(Csv)));
        foreach (var row in data.Rows) builder.AppendLine(string.Join(';', row.Select(Csv)));
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static byte[] ToExcel(ReportDataset data)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Report");
        sheet.Cell(1, 1).Value = data.Title; sheet.Range(1, 1, 1, Math.Max(1, data.Headers.Count)).Merge().Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#166534"));
        sheet.Cell(2, 1).Value = data.Subtitle; sheet.Range(2, 1, 2, Math.Max(1, data.Headers.Count)).Merge().Style.Font.SetFontColor(XLColor.FromHtml("#475569"));
        for (var column = 0; column < data.Headers.Count; column++) { var cell = sheet.Cell(4, column + 1); cell.Value = data.Headers[column]; cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7"); }
        for (var row = 0; row < data.Rows.Count; row++) for (var column = 0; column < data.Headers.Count; column++) sheet.Cell(row + 5, column + 1).SetValue(Safe(data.Rows[row].ElementAtOrDefault(column) ?? ""));
        sheet.SheetView.FreezeRows(4); sheet.Range(4, 1, Math.Max(4, data.Rows.Count + 4), data.Headers.Count).SetAutoFilter(); sheet.Columns().AdjustToContents(8, 45);
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    public static byte[] ToPdf(ReportDataset data)
    {
        const float pageWidth = 842, pageHeight = 595, margin = 34, headerHeight = 78, rowHeight = 20; var rowIndex = 0; var pageNumber = 0;
        using var stream = new MemoryStream(); using var document = SKDocument.CreatePdf(stream); using var regularTypeface = SKTypeface.FromFile(AlmerPdfFont.Find(false)); using var boldTypeface = SKTypeface.FromFile(AlmerPdfFont.Find(true)); using var body = new SKFont(regularTypeface, 8); using var bold = new SKFont(boldTypeface, 8); using var title = new SKFont(boldTypeface, 17); using var small = new SKFont(regularTypeface, 7); using var dark = new SKPaint { Color = SKColors.Black, IsAntialias = true }; using var green = new SKPaint { Color = new SKColor(22, 101, 52), IsAntialias = true }; using var gray = new SKPaint { Color = SKColors.Gray, IsAntialias = true }; using var header = new SKPaint { Color = new SKColor(220, 252, 231) }; using var alternate = new SKPaint { Color = new SKColor(248, 250, 252) }; using var line = new SKPaint { Color = new SKColor(203, 213, 225), StrokeWidth = 1 };
        do
        {
            using var canvas = document.BeginPage(pageWidth, pageHeight); pageNumber++; var width = pageWidth - margin * 2; var columnWidth = width / Math.Max(1, data.Headers.Count); var y = margin;
            canvas.DrawText(data.Title, margin, y + title.Size, title, green); y += 25; canvas.DrawText(data.Subtitle, margin, y + body.Size, body, gray); y = margin + headerHeight;
            canvas.DrawRect(margin, y, width, rowHeight, header); for (var column = 0; column < data.Headers.Count; column++) DrawCell(canvas, data.Headers[column], bold, dark, margin + column * columnWidth, y, columnWidth, rowHeight); y += rowHeight;
            while (rowIndex < data.Rows.Count && y + rowHeight < pageHeight - 36)
            {
                if (rowIndex % 2 == 1) canvas.DrawRect(margin, y, width, rowHeight, alternate);
                for (var column = 0; column < data.Headers.Count; column++) DrawCell(canvas, data.Rows[rowIndex].ElementAtOrDefault(column) ?? "", body, dark, margin + column * columnWidth, y, columnWidth, rowHeight);
                rowIndex++; y += rowHeight;
            }
            canvas.DrawLine(margin, pageHeight - 30, pageWidth - margin, pageHeight - 30, line); canvas.DrawText($"Almer Business Suite | {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC", margin, pageHeight - 15, small, gray); var pageText = pageNumber.ToString(); canvas.DrawText(pageText, pageWidth - margin - small.MeasureText(pageText), pageHeight - 15, small, gray); document.EndPage();
        } while (rowIndex < data.Rows.Count || pageNumber == 0);
        document.Close(); return stream.ToArray();
    }

    private static void DrawCell(SKCanvas canvas, string value, SKFont font, SKPaint paint, float x, float y, float width, float height)
    {
        var text = value; while (text.Length > 1 && font.MeasureText(text) > width - 8) text = text[..^1]; if (text.Length < value.Length) text = text.Length > 3 ? text[..^3] + "..." : "..."; canvas.DrawText(text, x + 4, y + height / 2 + font.Size * .35f, font, paint);
    }
    private static string Csv(string value) { value = Safe(value); return $"\"{value.Replace("\"", "\"\"")}\""; }
    private static string Safe(string value) => value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
}

public static class AlmerPdfFont
{
    public static string Find(bool bold)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), bold ? "arialbd.ttf" : "arial.ttf") }
            : new[] { bold ? "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" : "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" };
        return candidates.FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("A Unicode report font is not installed.");
    }
}
