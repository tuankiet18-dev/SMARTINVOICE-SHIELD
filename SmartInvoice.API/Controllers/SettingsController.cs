using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.DTOs.Settings;
using SmartInvoice.API.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartInvoice.API.Controllers
{
    [Route("api/settings")]
    [ApiController]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ICompanyService _companyService;
        private readonly IUserService _userService;

        public SettingsController(ICompanyService companyService, IUserService userService)
        {
            _companyService = companyService;
            _userService = userService;
        }

        // ==========================================
        // Company Settings Endpoints
        // ==========================================

        [HttpGet("company")]
        [Authorize(Policy = Constants.Permissions.CompanyManage)]
        public async Task<IActionResult> GetCompanySettings()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !Guid.TryParse(companyIdClaim, out var companyId))
                return BadRequest(new { Message = "Company ID not found in token." });

            var company = await _companyService.GetByIdAsync(companyId);
            if (company == null) return NotFound(new { Message = "Company not found." });

            var dto = new CompanySettingsDto
            {
                CompanyId = company.CompanyId,
                CompanyName = company.CompanyName,
                TaxCode = company.TaxCode,
                Address = company.Address,
                PhoneNumber = company.PhoneNumber,
                IsAutoApproveEnabled = company.IsAutoApproveEnabled,
                AutoApproveThreshold = company.AutoApproveThreshold
            };

            return Ok(dto);
        }

        [HttpPut("company")]
        [Authorize(Policy = Constants.Permissions.CompanyManage)]
        public async Task<IActionResult> UpdateCompanySettings([FromBody] UpdateCompanySettingsDto request)
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !Guid.TryParse(companyIdClaim, out var companyId))
                return BadRequest(new { Message = "Company ID not found in token." });

            var company = await _companyService.GetByIdAsync(companyId);
            if (company == null) return NotFound(new { Message = "Company not found." });

            company.IsAutoApproveEnabled = request.IsAutoApproveEnabled;
            // threshold should not be negative
            company.AutoApproveThreshold = request.AutoApproveThreshold >= 0 ? request.AutoApproveThreshold : 0;
            company.UpdatedAt = DateTime.UtcNow;

            await _companyService.UpdateAsync(company);

            return Ok(new { Message = "Company settings updated successfully." });
        }

        // ==========================================
        // User Settings Endpoints
        // ==========================================

        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfileSettings()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { Message = "User ID not found in token." });

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound(new { Message = "User not found." });

            var dto = new UserSettingsDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                EmployeeId = user.EmployeeId,
                ReceiveEmailNotifications = user.ReceiveEmailNotifications,
                ReceiveInAppNotifications = user.ReceiveInAppNotifications
            };

            return Ok(dto);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateUserProfileSettings([FromBody] UpdateUserSettingsDto request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { Message = "User ID not found in token." });

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound(new { Message = "User not found." });

            user.FullName = request.FullName;
            user.EmployeeId = request.EmployeeId;
            user.ReceiveEmailNotifications = request.ReceiveEmailNotifications;
            user.ReceiveInAppNotifications = request.ReceiveInAppNotifications;
            user.UpdatedAt = DateTime.UtcNow;

            await _userService.UpdateUserAsync(user);

            return Ok(new { Message = "User profile settings updated successfully." });
        }
    }
}
