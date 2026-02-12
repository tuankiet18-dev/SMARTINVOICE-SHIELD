using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartInvoice.API.DTOs
{
    public class UploadRequestDto
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
    }
}
