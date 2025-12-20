using System;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class TableSection
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Section Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }
    }
}
