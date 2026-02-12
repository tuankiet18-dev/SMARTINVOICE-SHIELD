using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Services;

namespace SmartInvoice.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly StorageService _storageService;
        private readonly AppDbContext _context;

        public InvoicesController(StorageService storageService, AppDbContext context)
        {
            _storageService = storageService;
            _context = context;
        }

        // API 1: Lấy link upload (Frontend gọi cái này trước)
        [HttpPost("generate-upload-url")]
        public IActionResult GetUploadUrl([FromBody] UploadRequestDto request)
        {
            var url = _storageService.GeneratePresignedUrl(request.FileName, request.ContentType);
            // Trả về URL để FE dùng PUT upload file
            return Ok(new { UploadUrl = url });
        }

        // API 2: Tạo hóa đơn (Gọi sau khi upload xong)
        [HttpPost]
        public async Task<IActionResult> CreateInvoice()
        {
            // Tạm thời hard-code để test, sau này sẽ lấy từ Body gửi lên
            var invoice = new Invoice
            {
                InvoiceId = Guid.NewGuid(),
                InvoiceNumber = "INV-TEST-01",
                Status = "Pending",
                CompanyId = Guid.NewGuid(), // Fake ID: Will need a real company in DB to save
                UploadedBy = Guid.Empty // Placeholder: Will be replaced by actual user ID from context
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return Ok(invoice);
        }
    }
}