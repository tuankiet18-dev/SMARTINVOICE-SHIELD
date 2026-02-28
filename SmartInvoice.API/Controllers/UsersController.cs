using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.DTOs.User;
using SmartInvoice.API.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartInvoice.API.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { Message = "User ID not found in token." });
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var userProfile = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                EmployeeId = user.EmployeeId,
                CompanyId = user.CompanyId,
                Role = user.Role,
                Permissions = user.Permissions,
                IsActive = user.IsActive
            };

            return Ok(userProfile);
        }

        [HttpGet]
        [Authorize(Roles = "CompanyAdmin")] // Or any role that can view users
        public async Task<IActionResult> GetUsers()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !Guid.TryParse(companyIdClaim, out var companyId))
            {
                return BadRequest(new { Message = "Company ID not found in token." });
            }

            var users = await _userService.GetUsersByCompanyIdAsync(companyId);

            var userList = users.Select(user => new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                EmployeeId = user.EmployeeId,
                CompanyId = user.CompanyId,
                Role = user.Role,
                Permissions = user.Permissions,
                IsActive = user.IsActive
            });

            return Ok(userList);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            // Only the user themselves or an Admin can update
            var currentUserIdStr = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(currentUserIdStr) || !Guid.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized(new { Message = "Unauthorized." });
            }

            if (currentUserId != id && currentUserRole != "CompanyAdmin")
            {
                return Forbid();
            }

            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            // Ensure admin doesn't update users in another company
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value;
            if (currentUserRole == "CompanyAdmin" && Guid.TryParse(companyIdClaim, out var claimsCompanyId))
            {
                if (user.CompanyId != claimsCompanyId)
                {
                    return Forbid();
                }
            }

            user.FullName = request.FullName;
            user.EmployeeId = request.EmployeeId;
            user.UpdatedAt = DateTime.UtcNow;

            // Save changes
            await _userService.UpdateUserAsync(user);

            var updatedProfile = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                EmployeeId = user.EmployeeId,
                CompanyId = user.CompanyId,
                Role = user.Role,
                Permissions = user.Permissions,
                IsActive = user.IsActive
            };

            return Ok(updatedProfile);
        }
    }
}
