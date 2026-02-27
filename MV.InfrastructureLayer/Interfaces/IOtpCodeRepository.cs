using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface IOtpCodeRepository
    {
        Task<OtpCode?> GetValidOtpAsync(string email, string type);
        Task<int> CountRecentOtpAsync(string email, string type, int minutesWindow);
        Task InvalidateAllOtpAsync(string email, string type);
        Task CreateAsync(OtpCode otpCode);
        Task MarkAsUsedAsync(int otpId);
    }
}
