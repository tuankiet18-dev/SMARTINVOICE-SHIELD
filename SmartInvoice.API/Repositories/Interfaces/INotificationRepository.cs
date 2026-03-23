using SmartInvoice.API.Entities;
namespace SmartInvoice.API.Repositories.Interfaces
{
    public interface INotificationRepository : IGenericRepository<Notification>
    {
        Task<IEnumerable<Notification>> GetByUserIdAsync(System.Guid userId, bool unreadOnly, int pageIndex, int pageSize);
        Task<int> GetUnreadCountAsync(System.Guid userId);
    }
}
