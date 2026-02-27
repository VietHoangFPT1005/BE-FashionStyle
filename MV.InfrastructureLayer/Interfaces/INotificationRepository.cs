using MV.DomainLayer.Entities;

namespace MV.InfrastructureLayer.Interfaces
{
    public interface INotificationRepository
    {
        Task<Notification> CreateAsync(Notification notification);
    }
}
