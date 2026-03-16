using System.Threading.Tasks;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface ISystemConfigProvider
    {
        Task<string> GetStringAsync(string key, string defaultValue = "");
        Task<int> GetIntAsync(string key, int defaultValue = 0);
        Task<bool> GetBoolAsync(string key, bool defaultValue = false);
        Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m);
        Task ClearCacheAsync(string key);
    }
}
