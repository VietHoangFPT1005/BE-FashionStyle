using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly FashionDbContext _context;

        public PaymentRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<Payment> CreateAsync(Payment payment)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task UpdateAsync(Payment payment)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
        }

        public async Task<Payment?> GetByOrderIdAsync(int orderId)
        {
            return await _context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.OrderId == orderId);
        }

        public async Task<Payment?> GetByOrderCodeAsync(string orderCode)
        {
            return await _context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.Order.OrderCode == orderCode);
        }

        public async Task<List<Payment>> GetExpiredPendingSePayPaymentsAsync()
        {
            return await _context.Payments
                .Include(p => p.Order)
                    .ThenInclude(o => o.OrderItems)
                .Where(p => p.PaymentMethod == "SEPAY"
                    && p.Status == "PENDING"
                    && p.ExpiredAt != null
                    && p.ExpiredAt < DateTime.Now)
                .ToListAsync();
        }
        public async Task<List<Payment>> GetPendingSePayPaymentsAsync()
        {
            return await _context.Payments
                .Include(p => p.Order)
                .Where(p => p.PaymentMethod == "SEPAY"
                    && p.Status == "PENDING"
                    && (p.ExpiredAt == null || p.ExpiredAt > DateTime.Now))
                .ToListAsync();
        }
    }
}
