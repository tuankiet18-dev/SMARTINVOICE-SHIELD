using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SmartInvoice.API.Data;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class SystemConfigProvider : ISystemConfigProvider
    {
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        public SystemConfigProvider(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        public async Task<string> GetStringAsync(string key, string defaultValue = "")
        {
            string cacheKey = $"SystemConfig_{key}";
            if (!_cache.TryGetValue(cacheKey, out string? value))
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var config = await context.SystemConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ConfigKey == key);

                value = config?.ConfigValue ?? defaultValue;

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(CacheDuration);

                _cache.Set(cacheKey, value, cacheEntryOptions);
            }

            return value ?? defaultValue;
        }

        public async Task<int> GetIntAsync(string key, int defaultValue = 0)
        {
            var value = await GetStringAsync(key, defaultValue.ToString());
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
        {
            var value = await GetStringAsync(key, defaultValue.ToString());
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m)
        {
            var value = await GetStringAsync(key, defaultValue.ToString());
            return decimal.TryParse(value, out decimal result) ? result : defaultValue;
        }

        public Task ClearCacheAsync(string key)
        {
            string cacheKey = $"SystemConfig_{key}";
            _cache.Remove(cacheKey);
            return Task.CompletedTask;
        }
    }
}
