using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.DTOs.Payment;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly IVnPayService _vnPayService;

    public PaymentController(IVnPayService vnPayService)
    {
        _vnPayService = vnPayService;
    }

    private (Guid UserId, Guid CompanyId) GetUserInfo()
    {
        var userIdStr = User.FindFirst("UserId")?.Value;
        var companyIdStr = User.FindFirst("CompanyId")?.Value;

        if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(companyIdStr))
            throw new UnauthorizedAccessException("User identity or company information is missing.");

        return (Guid.Parse(userIdStr), Guid.Parse(companyIdStr));
    }

    /// <summary>
    /// Get all available subscription packages
    /// </summary>
    [HttpGet("packages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPackages()
    {
        var packages = await _vnPayService.GetPackagesAsync();
        return Ok(packages);
    }

    /// <summary>
    /// Get current subscription info for the company
    /// </summary>
    [HttpGet("current-subscription")]
    [Authorize]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        try
        {
            var (_, companyId) = GetUserInfo();
            var subscription = await _vnPayService.GetCurrentSubscriptionAsync(companyId);
            return Ok(subscription);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Message = "User identity missing." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Create a VNPay payment URL for a subscription package
    /// </summary>
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        try
        {
            var (userId, companyId) = GetUserInfo();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var result = await _vnPayService.CreatePaymentAsync(request, companyId, userId, ipAddress);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Message = "User identity missing." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Handle VNPay return callback (called by VNPay after payment)
    /// </summary>
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn()
    {
        try
        {
            var vnpayData = Request.Query
                .ToDictionary(q => q.Key, q => q.Value.ToString());

            var result = await _vnPayService.ProcessVnPayReturnAsync(vnpayData);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Get payment history for the company
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetPaymentHistory()
    {
        try
        {
            var (_, companyId) = GetUserInfo();
            var history = await _vnPayService.GetPaymentHistoryAsync(companyId);
            return Ok(history);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Message = "User identity missing." });
        }
    }

    /// <summary>
    /// Get available add-ons
    /// </summary>
    [HttpGet("addons")]
    [Authorize]
    public IActionResult GetAddons()
    {
        var addons = _vnPayService.GetAvailableAddons();
        return Ok(addons);
    }

    /// <summary>
    /// Create a VNPay payment URL for an add-on purchase
    /// </summary>
    [HttpPost("addon/create")]
    [Authorize]
    public async Task<IActionResult> CreateAddonPayment([FromBody] CreateAddonPaymentRequest request)
    {
        try
        {
            var (userId, companyId) = GetUserInfo();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var result = await _vnPayService.CreateAddonPaymentAsync(request, companyId, userId, ipAddress);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Message = "User identity missing." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
