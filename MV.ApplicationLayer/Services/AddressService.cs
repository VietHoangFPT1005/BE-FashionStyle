using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Address.Request;
using MV.DomainLayer.DTOs.Address.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class AddressService : IAddressService
    {
        private readonly IUserAddressRepository _addressRepository;

        public AddressService(IUserAddressRepository addressRepository)
        {
            _addressRepository = addressRepository;
        }

        // ==================== API 15: Get Addresses ====================
        public async Task<ApiResponse<List<AddressResponse>>> GetAddressesAsync(int userId)
        {
            var addresses = await _addressRepository.GetByUserIdAsync(userId);

            var response = addresses.Select(MapToResponse).ToList();

            return ApiResponse<List<AddressResponse>>.SuccessResponse(response);
        }

        // ==================== API 16: Add New Address ====================
        public async Task<ApiResponse<AddressResponse>> CreateAddressAsync(int userId, CreateAddressRequest request)
        {
            // Check if this is the first address → auto set as default
            var count = await _addressRepository.CountByUserIdAsync(userId);
            var isFirstAddress = count == 0;

            // If setting as default, reset all other defaults first
            if (request.IsDefault && !isFirstAddress)
            {
                await _addressRepository.ResetDefaultAsync(userId);
            }

            var address = new UserAddress
            {
                UserId = userId,
                ReceiverName = request.ReceiverName,
                Phone = request.Phone,
                AddressLine = request.AddressLine,
                Ward = request.Ward,
                District = request.District,
                City = request.City,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsDefault = isFirstAddress || request.IsDefault
            };

            var created = await _addressRepository.CreateAsync(address);
            var response = MapToResponse(created);

            return ApiResponse<AddressResponse>.SuccessResponse(response, "Address added successfully.");
        }

        // ==================== API 17: Update Address ====================
        public async Task<ApiResponse<object>> UpdateAddressAsync(int userId, int addressId, UpdateAddressRequest request)
        {
            var address = await _addressRepository.GetByIdAndUserIdAsync(addressId, userId);
            if (address == null)
                return ApiResponse<object>.ErrorResponse("Address not found.");

            // If setting as default, reset all others
            if (request.IsDefault && address.IsDefault != true)
            {
                await _addressRepository.ResetDefaultAsync(userId);
            }

            address.ReceiverName = request.ReceiverName;
            address.Phone = request.Phone;
            address.AddressLine = request.AddressLine;
            address.Ward = request.Ward;
            address.District = request.District;
            address.City = request.City;
            address.Latitude = request.Latitude;
            address.Longitude = request.Longitude;
            address.IsDefault = request.IsDefault;

            await _addressRepository.UpdateAsync(address);

            return ApiResponse<object>.SuccessResponse(null, "Address updated successfully.");
        }

        // ==================== API 18: Delete Address ====================
        public async Task<ApiResponse<object>> DeleteAddressAsync(int userId, int addressId)
        {
            var address = await _addressRepository.GetByIdAndUserIdAsync(addressId, userId);
            if (address == null)
                return ApiResponse<object>.ErrorResponse("Address not found.");

            var wasDefault = address.IsDefault == true;

            await _addressRepository.DeleteAsync(address);

            // If deleted address was default, set the first remaining address as default
            if (wasDefault)
            {
                var remaining = await _addressRepository.GetByUserIdAsync(userId);
                if (remaining.Any())
                {
                    var first = remaining.First();
                    first.IsDefault = true;
                    await _addressRepository.UpdateAsync(first);
                }
            }

            return ApiResponse<object>.SuccessResponse(null, "Address deleted successfully.");
        }

        // ==================== API 19: Set Default Address ====================
        public async Task<ApiResponse<object>> SetDefaultAddressAsync(int userId, int addressId)
        {
            var address = await _addressRepository.GetByIdAndUserIdAsync(addressId, userId);
            if (address == null)
                return ApiResponse<object>.ErrorResponse("Address not found.");

            await _addressRepository.SetDefaultAsync(addressId, userId);

            return ApiResponse<object>.SuccessResponse(null, "Default address set successfully.");
        }

        private static AddressResponse MapToResponse(UserAddress address)
        {
            return new AddressResponse
            {
                AddressId = address.Id,
                ReceiverName = address.ReceiverName,
                Phone = address.Phone,
                AddressLine = address.AddressLine,
                Ward = address.Ward,
                District = address.District,
                City = address.City,
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                IsDefault = address.IsDefault == true,
                CreatedAt = address.CreatedAt
            };
        }
    }
}
