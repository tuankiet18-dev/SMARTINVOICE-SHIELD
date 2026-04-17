using System;
using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Invoice
{
    public class GetInvoicesQueryDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int Size { get; set; } = 10;

        public string? Keyword { get; set; }
        public string? Status { get; set; }
        public string? RiskLevel { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        
        // Thêm tham số để ẩn/hiện Hóa đơn Test của Data Seeder
        public bool ExcludeDemoData { get; set; } = false;
    }
}
