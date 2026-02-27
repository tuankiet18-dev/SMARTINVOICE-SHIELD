using System.Collections.Generic;
using System.Linq;

namespace SmartInvoice.API.DTOs.Invoice
{
    public class ValidationResultDto
    {
        public bool IsValid => !Errors.Any();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string SignerSubject { get; set; }

        // This holds the actual data parsed from the invoice (LineItems, etc.)
        public SmartInvoice.API.Entities.JsonModels.InvoiceExtractedData ExtractedData { get; set; }

        public void AddError(string error)
        {
            Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }
}
