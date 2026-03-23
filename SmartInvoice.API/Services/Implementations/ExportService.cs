using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs.Export;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Enums;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations;

public class ExportService : IExportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAwsS3Service _s3Service;
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public ExportService(
        IUnitOfWork unitOfWork,
        IAwsS3Service s3Service,
        AppDbContext dbContext,
        IWebHostEnvironment env
    )
    {
        _unitOfWork = unitOfWork;
        _s3Service = s3Service;
        _dbContext = dbContext;
        _env = env;
    }

    public async Task<ExportResultDto> GenerateExportAsync(
        Guid companyId,
        Guid userId,
        GenerateExportRequestDto request
    )
    {
        // 1. Tạo tên file thân thiện
        var startStr = request.StartDate.AddHours(7).ToString("ddMMyyyy");
        var endStr = request.EndDate.AddHours(7).ToString("ddMMyyyy");

        var fileName = $"Bao_cao_{request.ExportType}_{startStr}_{endStr}.xlsx";

        // 2. Tạo record ExportHistory với Status = Processing
        var filterCriteria = JsonSerializer.Serialize(
            new
            {
                request.StartDate,
                request.EndDate,
                request.InvoiceStatus,
                request.ExportType,
            }
        );

        var exportHistory = new ExportHistory
        {
            ExportId = Guid.NewGuid(),
            CompanyId = companyId,
            ExportedBy = userId,
            ExportFormat = "EXCEL",
            FileType = request.ExportType,
            FilterCriteria = filterCriteria,
            FileName = fileName,
            Status = ExportStatus.Processing.ToString(),
            ExportedAt = DateTime.UtcNow,
        };

        await _unitOfWork.ExportHistories.AddAsync(exportHistory);
        await _unitOfWork.CompleteAsync();

        try
        {
            // 3. Query DB lấy danh sách hóa đơn theo Filter (AsNoTracking để tối ưu memory)
            var query = _dbContext
                .Invoices.AsNoTracking()
                .Where(i =>
                    i.CompanyId == companyId
                    && i.InvoiceDate >= request.StartDate
                    && i.InvoiceDate <= request.EndDate
                );

            if (!string.IsNullOrEmpty(request.InvoiceStatus))
            {
                query = query.Where(i => i.Status == request.InvoiceStatus);
            }

            var invoices = await query.OrderBy(i => i.InvoiceDate).ToListAsync();

            var totalRecords = invoices.Count;

            // 4. Lấy ExportConfig của company
            var exportConfig = await _unitOfWork.ExportConfigs.GetByCompanyIdAsync(companyId);

            // 5. Sinh file Excel
            using var outputStream = new MemoryStream();

            if (request.ExportType.Equals("MISA", StringComparison.OrdinalIgnoreCase))
            {
                GenerateMisaExcel(invoices, exportConfig, outputStream);
            }
            else
            {
                GenerateStandardExcel(invoices, outputStream);
            }

            outputStream.Position = 0;
            var fileSize = outputStream.Length;

            // 6. Upload lên S3
            var s3Key = await _s3Service.UploadExportFileAsync(
                outputStream,
                fileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                companyId
            );

            // 7. Cập nhật ExportHistory thành Completed
            exportHistory.Status = ExportStatus.Completed.ToString();
            exportHistory.S3Key = s3Key;
            exportHistory.TotalRecords = totalRecords;
            exportHistory.FileSize = fileSize;
            exportHistory.ExpiresAt = DateTime.UtcNow.AddDays(7);

            _unitOfWork.ExportHistories.Update(exportHistory);
            await _unitOfWork.CompleteAsync();

            // 8. Generate Pre-signed URL
            var downloadUrl = _s3Service.GeneratePreSignedUrl(s3Key, expireMinutes: 15);

            return new ExportResultDto
            {
                ExportId = exportHistory.ExportId,
                FileName = fileName,
                Status = exportHistory.Status,
                TotalRecords = totalRecords,
                DownloadUrl = downloadUrl,
                ExpiresAt = exportHistory.ExpiresAt,
            };
        }
        catch (Exception)
        {
            exportHistory.Status = ExportStatus.Failed.ToString();
            _unitOfWork.ExportHistories.Update(exportHistory);
            await _unitOfWork.CompleteAsync();
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  MISA AMIS Accounting Template Export
    // ═══════════════════════════════════════════════════════════════
    private void GenerateMisaExcel(
        List<Invoice> invoices,
        ExportConfig? config,
        MemoryStream output
    )
    {
        var templatePath = Path.Combine(
            _env.ContentRootPath,
            "Template",
            "mua_hang_trong_nuoc_da_tien_te_full.xlsx"
        );
        using var workbook = new XLWorkbook(templatePath);
        var ws = workbook.Worksheets.First();

        // Dữ liệu bắt đầu từ dòng 9 (dòng 8 là header, dòng 9+ là ví dụ - ta ghi đè)
        const int startRow = 9;

        // Xóa các dòng ví dụ có sẵn trong template (nếu có)
        var lastUsedRow = ws.LastRowUsed()?.RowNumber() ?? startRow;
        if (lastUsedRow >= startRow)
        {
            ws.Rows(startRow, lastUsedRow).Delete();
        }

        int currentRow = startRow;

        foreach (var inv in invoices)
        {
            var lineItems = inv.ExtractedData?.LineItems;
            bool hasLineItems = lineItems != null && lineItems.Any();
            bool isSalesInvoice =
                !string.IsNullOrEmpty(inv.FormNumber) && inv.FormNumber.StartsWith("2");
            string? maKho = config?.DefaultWarehouse;

            // Nếu AI không đọc được LineItems nào, tạo 1 list giả chứa tổng tiền để tránh rớt mất hóa đơn
            // if (!hasLineItems)
            // {
            //     lineItems = new List<LineItemJson>
            //     {
            //         new LineItemJson
            //         {
            //             ProductName = $"Hàng hóa/dịch vụ theo HĐ {inv.InvoiceNumber}",
            //             Quantity = 1,
            //             UnitPrice = inv.TotalAmountBeforeTax ?? inv.TotalAmount,
            //             TotalAmount = inv.TotalAmountBeforeTax ?? inv.TotalAmount,
            //             VatAmount = inv.TotalTaxAmount ?? 0,
            //         },
            //     };
            // }

            foreach (var item in lineItems)
            {
                // ── Thông tin chung ──
                // Col A (1): Hình thức mua hàng
                // Col AE (31): Mã kho
                if (!string.IsNullOrEmpty(maKho))
                {
                    ws.Cell(currentRow, 1).Value = "Mua hàng trong nước nhập kho"; // Đúng format dropdown của MISA
                    ws.Cell(currentRow, 31).Value = maKho; // Cột AE: Mã kho
                }
                else
                {
                    ws.Cell(currentRow, 1).Value = "Mua hàng trong nước không qua kho";
                    ws.Cell(currentRow, 31).Value = ""; // Bỏ trống
                }

                // Col B (2): Phương thức thanh toán
                ws.Cell(currentRow, 2).Value = inv.PaymentMethod ?? "Chưa thanh toán";
                // Col C (3): Nhận kèm hóa đơn
                ws.Cell(currentRow, 3).Value = "Nhận kèm hóa đơn";

                // Col D (4): Ngày hạch toán (*)
                ws.Cell(currentRow, 4).Value = inv.InvoiceDate;
                ws.Cell(currentRow, 4).Style.DateFormat.Format = "dd/MM/yyyy";

                // Col E (5): Ngày chứng từ (*)
                ws.Cell(currentRow, 5).Value = inv.InvoiceDate;
                ws.Cell(currentRow, 5).Style.DateFormat.Format = "dd/MM/yyyy";

                // Col F (6): Số phiếu nhập (*)  - dùng InvoiceNumber
                ws.Cell(currentRow, 6).Value = inv.InvoiceNumber; // Số phiếu nhập = Số HĐ

                // ── Thông tin hóa đơn & NCC ──
                // Col H (8): Mẫu số HĐ
                ws.Cell(currentRow, 8).Value = inv.FormNumber;
                // Col I (9): Ký hiệu HĐ
                ws.Cell(currentRow, 9).Value = inv.SerialNumber;
                // Col J (10): Số hóa đơn
                ws.Cell(currentRow, 10).Value = inv.InvoiceNumber;
                // Col K (11): Ngày hóa đơn
                ws.Cell(currentRow, 11).Value = inv.InvoiceDate;
                ws.Cell(currentRow, 11).Style.DateFormat.Format = "dd/MM/yyyy";

                // Col O (15): Tên nhà cung cấp
                ws.Cell(currentRow, 15).Value = inv.Seller.Name;
                // Col P (16): Địa chỉ
                ws.Cell(currentRow, 16).Value = inv.Seller.Address;
                // Col Q (17): Mã số thuế
                ws.Cell(currentRow, 17).Value = inv.Seller.TaxCode;

                // ── Diễn giải ──
                // Col S (19): Diễn giải
                ws.Cell(currentRow, 19).Value =
                    $"Mua hàng theo HĐ {inv.InvoiceNumber} ngày {inv.InvoiceDate:dd/MM/yyyy}";

                // ── Tiền tệ ──
                // Col Z (26): Loại tiền
                ws.Cell(currentRow, 26).Value = inv.InvoiceCurrency;
                // Col AA (27): Tỷ giá
                ws.Cell(currentRow, 27).Value = inv.ExchangeRate;

                // ── CHI TIẾT HÀNG TIỀN (Lấy từ từng Item) ──
                // Col AC (29): Tên hàng - mô tả tổng
                ws.Cell(currentRow, 29).Value = string.IsNullOrEmpty(item.ProductName)
                    ? "Hàng hóa dịch vụ"
                    : item.ProductName;

                // Col AG (33): TK kho/TK chi phí (*)
                // Col AH (34): TK công nợ/TK tiền (*)
                // Cả 2 cột này nếu có config thì điền, nếu không có thì để trống (không mặc định TK nào cả)
                ws.Cell(currentRow, 33).Value = config?.DefaultDebitAccount;
                ws.Cell(currentRow, 34).Value = config?.DefaultCreditAccount;

                // Col AJ (36): Số lượng
                ws.Cell(currentRow, 36).Value = item.Quantity > 0 ? item.Quantity : 1;
                // Col AK (37): Đơn giá
                var unitPrice = item.UnitPrice > 0 ? item.UnitPrice : item.TotalAmount;
                ws.Cell(currentRow, 37).Value = unitPrice;
                ws.Cell(currentRow, 37).Style.NumberFormat.Format = "#,##0";
                // Col AL (38): Thành tiền
                ws.Cell(currentRow, 38).Value = item.TotalAmount;
                ws.Cell(currentRow, 38).Style.NumberFormat.Format = "#,##0";
                // Col AM (39): Thành tiền quy đổi
                ws.Cell(currentRow, 39).Value = item.TotalAmount * inv.ExchangeRate;
                ws.Cell(currentRow, 39).Style.NumberFormat.Format = "#,##0";

                // ── THUẾ GTGT TỪNG DÒNG ──
                if (isSalesInvoice)
                {
                    ws.Cell(currentRow, 43).Value = "";
                    ws.Cell(currentRow, 45).Value = 0;
                    ws.Cell(currentRow, 46).Value = 0;
                    ws.Cell(currentRow, 47).Value = "";
                }
                else
                {
                    // Col AQ (43): % thuế GTGT
                    // Lấy VatRate từ item (VD: "8", "10", "KCT", "0")
                    decimal vatRateNum = Convert.ToDecimal(item.VatRate);
                    if (vatRateNum > 0)
                    {
                        ws.Cell(currentRow, 43).Value = vatRateNum;
                    }
                    else
                    {
                        ws.Cell(currentRow, 43).Value = ""; // Nếu là KCT hoặc không rõ thì để trống
                    }
                    // Col AS (45): Tiền thuế GTGT
                    ws.Cell(currentRow, 45).Value = item.VatAmount;
                    ws.Cell(currentRow, 45).Style.NumberFormat.Format = "#,##0";
                    // Col AT (46): Tiền thuế GTGT quy đổi
                    ws.Cell(currentRow, 46).Value = item.VatAmount * inv.ExchangeRate;
                    ws.Cell(currentRow, 46).Style.NumberFormat.Format = "#,##0";
                    // Col AU (47): TK thuế GTGT
                    ws.Cell(currentRow, 47).Value = config?.DefaultTaxAccount;
                }

                currentRow++; // Tăng dòng Excel lên 1 cho món hàng tiếp theo
            }
        }

        workbook.SaveAs(output);
    }

    // ═══════════════════════════════════════════════════════════════
    //  STANDARD Export (Không cần template)
    // ═══════════════════════════════════════════════════════════════
    private void GenerateStandardExcel(List<Invoice> invoices, MemoryStream output)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Danh sach hoa don");

        // Header
        var headers = new[]
        {
            "STT",
            "Số hóa đơn",
            "Ký hiệu",
            "Ngày hóa đơn",
            "Tên NCC",
            "MST NCC",
            "Tiền trước thuế",
            "Tiền thuế",
            "Tổng tiền",
            "Loại tiền",
            "Trạng thái",
            "Mức rủi ro",
        };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a4b8c");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (int i = 0; i < invoices.Count; i++)
        {
            var inv = invoices[i];
            var row = i + 2;

            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = inv.InvoiceNumber;
            ws.Cell(row, 3).Value = inv.SerialNumber;
            ws.Cell(row, 4).Value = inv.InvoiceDate;
            ws.Cell(row, 4).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 5).Value = inv.Seller.Name;
            ws.Cell(row, 6).Value = inv.Seller.TaxCode;
            ws.Cell(row, 7).Value = inv.TotalAmountBeforeTax ?? 0;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 8).Value = inv.TotalTaxAmount ?? 0;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 9).Value = inv.TotalAmount;
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 10).Value = inv.InvoiceCurrency;
            ws.Cell(row, 11).Value = inv.Status;
            ws.Cell(row, 12).Value = inv.RiskLevel;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(output);
    }
}
