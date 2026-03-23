using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.DTOs.SystemConfig;
using SmartInvoice.API.Entities;

namespace SmartInvoice.API.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [ApiController]
    [Route("api/subscription-packages")]
    public class SubscriptionPackagesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubscriptionPackagesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        {
            var query = _context.SubscriptionPackages.AsQueryable();

            if (activeOnly)
            {
                query = query.Where(p => p.IsActive);
            }

            var packages = await query
                .OrderBy(p => p.PackageLevel)
                .Select(p => new SubscriptionPackageDto
                {
                    PackageId = p.PackageId,
                    PackageCode = p.PackageCode,
                    PackageName = p.PackageName,
                    Description = p.Description,
                    PricePerMonth = p.PricePerMonth,
                    PricePerSixMonths = p.PricePerSixMonths,
                    PricePerYear = p.PricePerYear,
                    MaxUsers = p.MaxUsers,
                    MaxInvoicesPerMonth = p.MaxInvoicesPerMonth,
                    StorageQuotaGB = p.StorageQuotaGB,
                    PackageLevel = p.PackageLevel,
                    HasAiProcessing = p.HasAiProcessing,
                    HasAdvancedWorkflow = p.HasAdvancedWorkflow,
                    HasRiskWarning = p.HasRiskWarning,
                    HasAuditLog = p.HasAuditLog,
                    HasErpIntegration = p.HasErpIntegration,
                    IsActive = p.IsActive
                })
                .ToListAsync();

            return Ok(packages);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var package = await _context.SubscriptionPackages.FindAsync(id);
            if (package == null)
                return NotFound(new { message = "Không tìm thấy gói cước." });

            return Ok(new SubscriptionPackageDto
            {
                PackageId = package.PackageId,
                PackageCode = package.PackageCode,
                PackageName = package.PackageName,
                Description = package.Description,
                PricePerMonth = package.PricePerMonth,
                PricePerSixMonths = package.PricePerSixMonths,
                PricePerYear = package.PricePerYear,
                MaxUsers = package.MaxUsers,
                MaxInvoicesPerMonth = package.MaxInvoicesPerMonth,
                StorageQuotaGB = package.StorageQuotaGB,
                PackageLevel = package.PackageLevel,
                HasAiProcessing = package.HasAiProcessing,
                HasAdvancedWorkflow = package.HasAdvancedWorkflow,
                HasRiskWarning = package.HasRiskWarning,
                HasAuditLog = package.HasAuditLog,
                HasErpIntegration = package.HasErpIntegration,
                IsActive = package.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionPackageDto dto)
        {
            if (await _context.SubscriptionPackages.AnyAsync(p => p.PackageCode == dto.PackageCode))
            {
                return BadRequest(new { message = "Mã gói cước đã tồn tại." });
            }

            var package = new SubscriptionPackage
            {
                PackageId = Guid.NewGuid(),
                PackageCode = dto.PackageCode,
                PackageName = dto.PackageName,
                Description = dto.Description,
                PricePerMonth = dto.PricePerMonth,
                PricePerSixMonths = dto.PricePerSixMonths,
                PricePerYear = dto.PricePerYear,
                MaxUsers = dto.MaxUsers,
                MaxInvoicesPerMonth = dto.MaxInvoicesPerMonth,
                StorageQuotaGB = dto.StorageQuotaGB,
                PackageLevel = dto.PackageLevel,
                HasAiProcessing = dto.HasAiProcessing,
                HasAdvancedWorkflow = dto.HasAdvancedWorkflow,
                HasRiskWarning = dto.HasRiskWarning,
                HasAuditLog = dto.HasAuditLog,
                HasErpIntegration = dto.HasErpIntegration,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.SubscriptionPackages.Add(package);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = package.PackageId }, new { message = "Đã tạo gói cước thành công.", id = package.PackageId });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPackageDto dto)
        {
            var package = await _context.SubscriptionPackages.FindAsync(id);
            if (package == null)
                return NotFound(new { message = "Không tìm thấy gói cước." });

            // Check if changing to a code that already exists (and isn't this one)
            if (package.PackageCode != dto.PackageCode && await _context.SubscriptionPackages.AnyAsync(p => p.PackageCode == dto.PackageCode))
            {
                return BadRequest(new { message = "Mã gói cước đã tồn tại." });
            }

            package.PackageCode = dto.PackageCode;
            package.PackageName = dto.PackageName;
            package.Description = dto.Description;
            package.PricePerMonth = dto.PricePerMonth;
            package.PricePerSixMonths = dto.PricePerSixMonths;
            package.PricePerYear = dto.PricePerYear;
            package.MaxUsers = dto.MaxUsers;
            package.MaxInvoicesPerMonth = dto.MaxInvoicesPerMonth;
            package.StorageQuotaGB = dto.StorageQuotaGB;
            package.PackageLevel = dto.PackageLevel;
            package.HasAiProcessing = dto.HasAiProcessing;
            package.HasAdvancedWorkflow = dto.HasAdvancedWorkflow;
            package.HasRiskWarning = dto.HasRiskWarning;
            package.HasAuditLog = dto.HasAuditLog;
            package.HasErpIntegration = dto.HasErpIntegration;
            package.IsActive = dto.IsActive;
            package.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã cập nhật gói cước thành công." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var package = await _context.SubscriptionPackages.FindAsync(id);
            if (package == null)
                return NotFound(new { message = "Không tìm thấy gói cước." });

            // Cannot delete if there are active companies using it
            var inUse = await _context.Companies.AnyAsync(c => c.SubscriptionPackageId == id && !c.IsDeleted);
            if (inUse)
            {
                return BadRequest(new { message = "Không thể xoá gói cước đang được sử dụng bởi các tenant. Vui lòng vô hiệu hóa gói cước thay vì xoá." });
            }

            // Soft-delete using IsActive since actual delete might break history
            package.IsActive = false;
            package.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã vô hiệu hóa gói cước thành công." });
        }
    }
}
