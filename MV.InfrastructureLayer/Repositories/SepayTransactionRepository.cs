// =====================================================================
// TODO: Uncomment sau khi chạy SQL script + scaffold lại database
// =====================================================================
using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class SepayTransactionRepository : ISepayTransactionRepository
    {
        private readonly FashionDbContext _context;

        public SepayTransactionRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<SepayTransaction> CreateAsync(SepayTransaction transaction)
        {
            _context.SepayTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task UpdateAsync(SepayTransaction transaction)
        {
            _context.SepayTransactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsBySepayIdAsync(string sepayId)
        {
            return await _context.SepayTransactions
                .AnyAsync(t => t.SepayId == sepayId);
        }
    }
}
