using System.ComponentModel.DataAnnotations;

namespace MV.DomainLayer.DTOs.Admin.Request
{
    public class ChangeRoleRequest
    {
        [Required(ErrorMessage = "Role is required.")]
        [Range(1, 4, ErrorMessage = "Role must be between 1 and 4 (1=Admin, 2=Staff, 3=Customer, 4=Shipper).")]
        public int Role { get; set; }
    }
}
