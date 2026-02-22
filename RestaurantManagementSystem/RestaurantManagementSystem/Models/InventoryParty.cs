using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class InventoryParty
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Party Code")]
        public string PartyCode { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [Display(Name = "Party Name")]
        public string PartyName { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [StringLength(150)]
        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [Display(Name = "Party Type")]
        [Required]
        public string PartyType { get; set; } = "Vendor";

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
