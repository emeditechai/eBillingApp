using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class CounterMaster
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Counter Code")]
        public string CounterCode { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        [Display(Name = "Counter Name")]
        public string CounterName { get; set; } = string.Empty;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
