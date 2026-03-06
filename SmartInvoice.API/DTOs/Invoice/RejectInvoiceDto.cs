using System.ComponentModel.DataAnnotations;

namespace SmartInvoice.API.DTOs.Invoice
{
    public class RejectInvoiceDto
    {
        [Required(ErrorMessage = "Lý do từ chối là bắt buộc.")]
        [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }
}
