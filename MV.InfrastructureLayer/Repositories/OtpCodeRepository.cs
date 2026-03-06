using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class OtpCodeRepository : IOtpCodeRepository
    {
        private readonly FashionDbContext _context;

        public OtpCodeRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<OtpCode?> GetValidOtpAsync(string email, string type)
        {
            return await _context.OtpCodes
                .Where(o => o.Email == email
                    && o.Type == type
                    && o.IsUsed == false
                    && o.ExpiredAt >= DateTime.Now)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<int> CountRecentOtpAsync(string email, string type, int minutesWindow)
        {
            var windowStart = DateTime.Now.AddMinutes(-minutesWindow);
            return await _context.OtpCodes
                .CountAsync(o => o.Email == email
                    && o.Type == type
                    && o.CreatedAt >= windowStart);
        }

        public async Task InvalidateAllOtpAsync(string email, string type)
        {
            var otps = await _context.OtpCodes
                .Where(o => o.Email == email && o.Type == type && o.IsUsed == false)
                .ToListAsync();

            foreach (var otp in otps)
            {
                otp.IsUsed = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task CreateAsync(OtpCode otpCode)
        {
            _context.OtpCodes.Add(otpCode);
            await _context.SaveChangesAsync();
        }

        public async Task MarkAsUsedAsync(int otpId)
        {
            var otp = await _context.OtpCodes.FindAsync(otpId);
            if (otp != null)
            {
                otp.IsUsed = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}
