using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using SmartInvoice.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SmartInvoice.API.Security
{
    public class ClaimsTransformer : IClaimsTransformation
    {
        private readonly IServiceProvider _serviceProvider;

        public ClaimsTransformer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            // If the principal is not authenticated, do nothing
            if (!principal.Identity?.IsAuthenticated ?? true)
                return principal;

            var claimsIdentity = principal.Identity as ClaimsIdentity;
            if (claimsIdentity == null)
                return principal;

            // Prevent infinite loop if we already transformed
            if (claimsIdentity.HasClaim(c => c.Type == "RolesLoaded"))
                return principal;

            // Extract user identifier (sub or email) from Cognito token
            var subClaim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier)
                ?? claimsIdentity.FindFirst("sub")
                ?? claimsIdentity.FindFirst("username")
                ?? claimsIdentity.FindFirst("cognito:username")
                ?? claimsIdentity.FindFirst(ClaimTypes.Email)
                ?? claimsIdentity.FindFirst("email");

            if (subClaim == null)
                return principal;

            var claimValue = subClaim.Value.ToLower();

            // We must use a separate scope to inject AppDbContext, 
            // because ClaimsAuthentication can run in varying scopes.
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.CognitoSub == subClaim.Value || u.Email == claimValue);

                if (user != null)
                {
                    // Add Role Claim
                    if (!string.IsNullOrEmpty(user.Role))
                    {
                        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
                    }

                    // Add custom Permission claims
                    if (user.Permissions != null && user.Permissions.Any())
                    {
                        foreach (var perm in user.Permissions)
                        {
                            claimsIdentity.AddClaim(new Claim("Permission", perm));
                        }
                    }

                    // Add Company ID for multi-tenant checks
                    if (user.CompanyId != Guid.Empty)
                    {
                        claimsIdentity.AddClaim(new Claim("CompanyId", user.CompanyId.ToString()));
                        claimsIdentity.AddClaim(new Claim("UserId", user.Id.ToString()));
                    }

                    // Mark as transformed
                    claimsIdentity.AddClaim(new Claim("RolesLoaded", "true"));
                }
            }

            return principal;
        }
    }
}
