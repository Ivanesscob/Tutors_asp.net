using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class Booking
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Урок обязателен")]
        [Display(Name = "Урок")]
        public int LessonId { get; set; }
        public virtual Lesson Lesson { get; set; }

        [Required(ErrorMessage = "Ученик обязателен")]
        [Display(Name = "Ученик")]
        public int StudentId { get; set; }
        public virtual User Student { get; set; }

        [Required(ErrorMessage = "Статус обязателен")]
        [Display(Name = "Статус")]
        public int StatusId { get; set; }
        public virtual BookingStatus Status { get; set; }
    }
} 