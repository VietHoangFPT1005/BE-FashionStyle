using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task CreateAsync(RefreshToken refreshToken);
        Task RevokeAsync(int tokenId);
        Task RevokeAllByUserIdAsync(int userId);
    }
}
