using Microsoft.EntityFrameworkCore;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;

namespace MV.InfrastructureLayer.Repositories
{
    public class UserBodyProfileRepository : IUserBodyProfileRepository
    {
        private readonly FashionDbContext _context;

        public UserBodyProfileRepository(FashionDbContext context)
        {
            _context = context;
        }

        public async Task<UserBodyProfile?> GetByUserIdAsync(int userId)
        {
            return await _context.UserBodyProfiles
                .FirstOrDefaultAsync(bp => bp.UserId == userId);
        }

        public async Task<bool> ExistsByUserIdAsync(int userId)
        {
            return await _context.UserBodyProfiles
                .AnyAsync(bp => bp.UserId == userId);
        }

        public async Task<UserBodyProfile> CreateAsync(UserBodyProfile profile)
        {
            _context.UserBodyProfiles.Add(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        public async Task UpdateAsync(UserBodyProfile profile)
        {
            _context.UserBodyProfiles.Update(profile);
            await _context.SaveChangesAsync();
        }
    }
}
