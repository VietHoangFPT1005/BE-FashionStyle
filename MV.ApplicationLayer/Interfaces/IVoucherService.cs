using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Voucher.Request;
using MV.DomainLayer.DTOs.Voucher.Response;

namespace MV.ApplicationLayer.ServiceInterfaces
{
    public interface IVoucherService
    {
        Task<ApiResponse<VoucherValidationResponse>> ValidateVoucherAsync(int userId, ValidateVoucherRequest request);
    }
}
