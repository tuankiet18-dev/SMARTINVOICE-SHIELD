using System.Threading.Tasks;
using SmartInvoice.API.DTOs.Invoice;
using SmartInvoice.API.Entities.JsonModels;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IInvoiceProcessorService
    {
        /// <summary>
        /// 1. Kiểm tra cấu trúc XML với file XSD
        /// </summary>
        ValidationResultDto ValidateStructure(string xmlPath);

        /// <summary>
        /// 2. Kiểm tra chữ ký số trên hóa đơn
        /// </summary>
        ValidationResultDto VerifyDigitalSignature(string xmlPath);

        /// <summary>
        /// 3. Bóc tách dữ liệu từ file XML
        /// </summary>
        InvoiceExtractedData ExtractData(string xmlPath);

        /// <summary>
        /// 4. Rà soát rủi ro logic, gọi API VietQR, kiểm tra tính toán
        /// </summary>
        Task<ValidationResultDto> ValidateBusinessLogicAsync(string xmlPath);
    }
}
