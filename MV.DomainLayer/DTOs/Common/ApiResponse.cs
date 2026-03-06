namespace MV.DomainLayer.DTOs.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static ApiResponse<T> SuccessResponse(T? data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> SuccessResponse(string message)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = default
            };
        }

        public static ApiResponse<T> ErrorResponse(string message)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default
            };
        }
    }

    public class ApiResponse : ApiResponse<object>
    {
        public new static ApiResponse SuccessResponse(string message)
        {
            return new ApiResponse
            {
                Success = true,
                Message = message,
                Data = null
            };
        }

        public new static ApiResponse ErrorResponse(string message)
        {
            return new ApiResponse
            {
                Success = false,
                Message = message,
                Data = null
            };
        }
    }
}
