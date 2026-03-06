using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class VoucherRepository : IVoucherRepository
    {
        private readonly FashionDbContext _context;

        public VoucherRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<Voucher?> GetByCodeAsync(string code)
        {
            return await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == code);
        }
    }
}
