using Microsoft.AspNetCore.Http;
using SmartInvoice.API.Services.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace SmartInvoice.API.Middlewares
{
    public class TenantStatusMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantStatusMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // Inject ICompanyService trực tiếp vào hàm InvokeAsync
        public async Task InvokeAsync(HttpContext context, ICompanyService companyService)
        {
            // Chỉ kiểm tra những user đã đăng nhập
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var companyIdClaim = context.User.FindFirst("CompanyId")?.Value;
                
                if (Guid.TryParse(companyIdClaim, out var companyId))
                {
                    var company = await companyService.GetByIdAsync(companyId);
                    
                    // Nếu công ty không tồn tại hoặc đã bị khóa -> Chặn lại, báo 403
                    if (company == null || !company.IsActive)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"message\": \"Doanh nghiệp của bạn đã bị khóa. Vui lòng liên hệ Admin.\"}");
                        return; // Dừng luôn, không cho đi tiếp vào Controller
                    }
                }
            }

            // Công ty vẫn Active -> Cho phép đi tiếp
            await _next(context);
        }
    }
}