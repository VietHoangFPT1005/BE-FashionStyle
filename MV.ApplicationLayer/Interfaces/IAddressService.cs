using MV.DomainLayer.DTOs.Address.Request;
using MV.DomainLayer.DTOs.Address.Response;
using MV.DomainLayer.DTOs.Common;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IAddressService
    {
        Task<ApiResponse<List<AddressResponse>>> GetAddressesAsync(int userId);
        Task<ApiResponse<AddressResponse>> CreateAddressAsync(int userId, CreateAddressRequest request);
        Task<ApiResponse<object>> UpdateAddressAsync(int userId, int addressId, UpdateAddressRequest request);
        Task<ApiResponse<object>> DeleteAddressAsync(int userId, int addressId);
        Task<ApiResponse<object>> SetDefaultAddressAsync(int userId, int addressId);
    }
}
