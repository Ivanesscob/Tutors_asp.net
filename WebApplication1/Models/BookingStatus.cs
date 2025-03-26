using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class BookingStatus
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }
    }
} 