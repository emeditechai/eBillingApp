using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class InventoryGodown
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Godown Code")]
        public string GodownCode { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        [Display(Name = "Godown Name")]
        public string GodownName { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Address { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
