using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
namespace SmartInvoice.API.Repositories.Implementations
{
    public class ValidationLayerRepository : BaseRepository<ValidationLayer>, IValidationLayerRepository
    {
        public ValidationLayerRepository(AppDbContext context) : base(context)
        {
        }
    }
}
