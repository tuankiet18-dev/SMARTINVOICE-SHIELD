using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
namespace SmartInvoice.API.Repositories.Implementations
{
    public class AIProcessingLogRepository : BaseRepository<AIProcessingLog>, IAIProcessingLogRepository
    {
        public AIProcessingLogRepository(AppDbContext context) : base(context)
        {
        }
    }
}
