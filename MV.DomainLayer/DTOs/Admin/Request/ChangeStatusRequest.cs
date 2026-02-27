using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Admin.Request
{
    public class ChangeStatusRequest
    {
        [Required(ErrorMessage = "IsActive is required.")]
        public bool IsActive { get; set; }
    }
}
