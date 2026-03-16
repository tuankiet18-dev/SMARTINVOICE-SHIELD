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
    [Route("api/system-config")]
    public class SystemConfigController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SystemConfigController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var configs = await _context.SystemConfigurations
                .Select(c => new SystemConfigDto
                {
                    ConfigId = c.ConfigId,
                    ConfigKey = c.ConfigKey,
                    ConfigValue = c.ConfigValue,
                    ConfigType = c.ConfigType,
                    Category = c.Category,
                    Description = c.Description,
                    DefaultValue = c.DefaultValue,
                    IsReadOnly = c.IsReadOnly,
                    RequiresRestart = c.RequiresRestart,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return Ok(configs);
        }

        [HttpPut("{configKey}")]
        public async Task<IActionResult> Update(string configKey, [FromBody] UpdateSystemConfigDto dto)
        {
            var config = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == configKey);
            if (config == null)
                return NotFound(new { message = "Không tìm thấy cấu hình này." });

            if (config.IsReadOnly)
                return BadRequest(new { message = "Cấu hình này chỉ đọc, không thể chỉnh sửa." });

            // Validate based on ConfigType
            if (config.ConfigType == "Integer" && !int.TryParse(dto.ConfigValue, out _))
                return BadRequest(new { message = "Giá trị phải là một số nguyên." });

            if (config.ConfigType == "Boolean" && !bool.TryParse(dto.ConfigValue, out _))
                return BadRequest(new { message = "Giá trị phải là True hoặc False." });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = userIdClaim != null ? Guid.Parse(userIdClaim) : (Guid?)null;

            // Log change
            var oldVal = config.ConfigValue;
            config.ConfigValue = dto.ConfigValue;
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedBy = userId;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã cập nhật cấu hình thành công.", requiresRestart = config.RequiresRestart });
        }
    }
}
