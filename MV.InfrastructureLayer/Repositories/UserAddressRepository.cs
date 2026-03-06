using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class UserAddressRepository : IUserAddressRepository
    {
        private readonly FashionDbContext _context;

        public UserAddressRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserAddress>> GetByUserIdAsync(int userId)
        {
            return await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserAddress?> GetByIdAndUserIdAsync(int addressId, int userId)
        {
            return await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);
        }

        public async Task<int> CountByUserIdAsync(int userId)
        {
            return await _context.UserAddresses
                .CountAsync(a => a.UserId == userId);
        }

        public async Task<UserAddress> CreateAsync(UserAddress address)
        {
            _context.UserAddresses.Add(address);
            await _context.SaveChangesAsync();
            return address;
        }

        public async Task UpdateAsync(UserAddress address)
        {
            _context.UserAddresses.Update(address);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(UserAddress address)
        {
            _context.UserAddresses.Remove(address);
            await _context.SaveChangesAsync();
        }

        public async Task ResetDefaultAsync(int userId)
        {
            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == userId && a.IsDefault == true)
                .ToListAsync();

            foreach (var addr in addresses)
            {
                addr.IsDefault = false;
            }

            await _context.SaveChangesAsync();
        }

        public async Task SetDefaultAsync(int addressId, int userId)
        {
            // Reset all defaults first
            var allAddresses = await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .ToListAsync();

            foreach (var addr in allAddresses)
            {
                addr.IsDefault = (addr.Id == addressId);
            }

            await _context.SaveChangesAsync();
        }
    }
}
