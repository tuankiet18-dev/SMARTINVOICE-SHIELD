using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvoice.API.Data;
using SmartInvoice.API.Data.Seeding;

namespace SmartInvoice.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestSeedController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TestSeedController> _logger;

    public TestSeedController(AppDbContext context, ILogger<TestSeedController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seed dummy invoices for the dashboard.
    /// ONLY FOR DEV/DEMO ENVIRONMENT!
    /// </summary>
    [HttpPost("seed-invoices")]
    [AllowAnonymous] // Allow hitting it simply from Swagger
    public async Task<IActionResult> SeedInvoices([FromQuery] int count = 500)
    {
        try
        {
            _logger.LogInformation("Starting to seed {Count} invoices...", count);
            
            await DashboardDataSeeder.SeedInvoicesAsync(_context, count);

            return Ok(new { message = $"Successfully seeded {count} invoices for Demo!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while seeding invoices.");
            return StatusCode(500, new { message = "Seeding failed.", error = ex.Message });
        }
    }
}