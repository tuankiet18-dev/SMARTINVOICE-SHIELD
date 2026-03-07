using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.DTOs.Blacklist;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/blacklist")]
[Authorize]
public class BlacklistController : ControllerBase
{
    private readonly ILocalBlacklistService _blacklistService;

    public BlacklistController(ILocalBlacklistService blacklistService)
    {
        _blacklistService = blacklistService;
    }

    /// <summary>
    /// Lấy danh sách tất cả công ty trong blacklist
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Constants.Permissions.BlacklistView)]
    public async Task<IActionResult> GetAll()
    {
        var items = await _blacklistService.GetAllAsync();

        var result = items.Select(MapToDto);

        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin công ty trong blacklist theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Constants.Permissions.BlacklistView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _blacklistService.GetByIdAsync(id);
        if (entity == null)
            return NotFound(new { Message = "Không tìm thấy công ty trong blacklist" });

        return Ok(MapToDto(entity));
    }

    /// <summary>
    /// Tìm công ty trong blacklist theo mã số thuế
    /// </summary>
    [HttpGet("tax-code/{taxCode}")]
    [Authorize(Policy = Constants.Permissions.BlacklistView)]
    public async Task<IActionResult> GetByTaxCode(string taxCode)
    {
        var entity = await _blacklistService.GetByTaxCodeAsync(taxCode);
        if (entity == null)
            return NotFound(new { Message = $"Không tìm thấy công ty với mã số thuế '{taxCode}' trong blacklist" });

        return Ok(MapToDto(entity));
    }

    /// <summary>
    /// Thêm công ty vào blacklist
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Constants.Permissions.BlacklistManage)]
    public async Task<IActionResult> Create([FromBody] CreateBlacklistDto dto)
    {
        // Kiểm tra mã số thuế đã tồn tại chưa
        var existing = await _blacklistService.GetByTaxCodeAsync(dto.TaxCode);
        if (existing != null)
        {
            if (existing.IsActive)
                return Conflict(new { Message = $"Mã số thuế '{dto.TaxCode}' đã tồn tại trong blacklist" });

            // Nếu đã bị xóa (IsActive = false), kích hoạt lại
            existing.IsActive = true;
            existing.CompanyName = dto.CompanyName ?? existing.CompanyName;
            existing.Reason = dto.Reason ?? existing.Reason;
            existing.RemovedDate = null;
            existing.AddedDate = DateTime.UtcNow;
            existing.AddedBy = GetCurrentUserId();

            await _blacklistService.UpdateAsync(existing);
            return Ok(MapToDto(existing));
        }

        var entity = new LocalBlacklistedCompany
        {
            BlacklistId = Guid.NewGuid(),
            TaxCode = dto.TaxCode,
            CompanyName = dto.CompanyName,
            Reason = dto.Reason,
            IsActive = true,
            AddedBy = GetCurrentUserId(),
            AddedDate = DateTime.UtcNow
        };

        var created = await _blacklistService.CreateAsync(entity);
        return CreatedAtAction(nameof(GetById), new { id = created.BlacklistId }, MapToDto(created));
    }

    /// <summary>
    /// Cập nhật thông tin công ty trong blacklist
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Constants.Permissions.BlacklistManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBlacklistDto dto)
    {
        var entity = await _blacklistService.GetByIdAsync(id);
        if (entity == null)
            return NotFound(new { Message = "Không tìm thấy công ty trong blacklist" });

        if (dto.CompanyName != null)
            entity.CompanyName = dto.CompanyName;

        if (dto.Reason != null)
            entity.Reason = dto.Reason;

        if (dto.IsActive.HasValue)
        {
            entity.IsActive = dto.IsActive.Value;
            if (!dto.IsActive.Value)
                entity.RemovedDate = DateTime.UtcNow;
            else
                entity.RemovedDate = null;
        }

        await _blacklistService.UpdateAsync(entity);
        return Ok(MapToDto(entity));
    }

    /// <summary>
    /// Xóa công ty khỏi blacklist (soft delete - set IsActive = false)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Constants.Permissions.BlacklistManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _blacklistService.GetByIdAsync(id);
        if (entity == null)
            return NotFound(new { Message = "Không tìm thấy công ty trong blacklist" });

        // Soft delete: chỉ đánh dấu IsActive = false
        entity.IsActive = false;
        entity.RemovedDate = DateTime.UtcNow;
        await _blacklistService.UpdateAsync(entity);

        return NoContent();
    }

    // ---- Helper Methods ----

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static BlacklistDto MapToDto(LocalBlacklistedCompany entity)
    {
        return new BlacklistDto
        {
            BlacklistId = entity.BlacklistId,
            TaxCode = entity.TaxCode,
            CompanyName = entity.CompanyName,
            Reason = entity.Reason,
            IsActive = entity.IsActive,
            AddedBy = entity.AddedBy,
            AddedDate = entity.AddedDate,
            RemovedDate = entity.RemovedDate
        };
    }
}
