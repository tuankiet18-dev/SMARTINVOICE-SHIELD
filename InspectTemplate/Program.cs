using ClosedXML.Excel;

var wb = new XLWorkbook(@"d:\FPT_Study\Semester_6_OJT_AWS\SmartInvoice_Project\SmartInvoice.API\Template\mua_hang_trong_nuoc_da_tien_te_full.xlsx");
var ws = wb.Worksheets.First();
Console.WriteLine($"Sheet: {ws.Name}");
Console.WriteLine($"Last row used: {ws.LastRowUsed()?.RowNumber()}");
Console.WriteLine($"Last col used: {ws.LastColumnUsed()?.ColumnNumber()}");

for (int row = 1; row <= 8; row++)
{
    Console.WriteLine($"\n--- Row {row} ---");
    for (int col = 1; col <= 60; col++)
    {
        var val = ws.Cell(row, col).GetString();
        if (!string.IsNullOrWhiteSpace(val))
        {
            string colLetter = GetColLetter(col);
            Console.WriteLine($"  Col {col} ({colLetter}): {val}");
        }
    }
}

static string GetColLetter(int col)
{
    string result = "";
    while (col > 0)
    {
        col--;
        result = (char)('A' + col % 26) + result;
        col /= 26;
    }
    return result;
}
