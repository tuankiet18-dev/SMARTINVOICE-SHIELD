using System.Net;
using System.Text.Json;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Middlewares
{
    public class MaintenanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MaintenanceMiddleware> _logger;

        public MaintenanceMiddleware(RequestDelegate next, ILogger<MaintenanceMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISystemConfigProvider configProvider)
        {
            // We allow GET requests and authentication-related endpoints to pass
            // to allow users to see the maintenance message or for admins to log in.
            // Adjust this logic if you want to block everything.
            if (context.Request.Method != "GET" &&
                !context.Request.Path.StartsWithSegments("/api/auth") &&
                !context.Request.Path.StartsWithSegments("/api/system-config")) // Allow admins to turn off maintenance mode
            {
                bool isMaintenanceMode = await configProvider.GetBoolAsync("MAINTENANCE_MODE", false);

                if (isMaintenanceMode)
                {
                    _logger.LogWarning("Blocking request to {Path} due to MAINTENANCE_MODE.", context.Request.Path);

                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    context.Response.ContentType = "application/json";

                    var response = new
                    {
                        message = "Hệ thống đang bảo trì. Vui lòng quay lại sau.",
                        status = "Maintenance"
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    return;
                }
            }

            await _next(context);
        }
    }
}
