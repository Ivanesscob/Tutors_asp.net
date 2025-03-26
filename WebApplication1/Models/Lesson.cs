using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    public class Lesson
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название урока обязательно")]
        [StringLength(100, ErrorMessage = "Название урока не должно превышать 100 символов")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Описание урока обязательно")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Цена урока обязательна")]
        [Range(0, 10000, ErrorMessage = "Цена должна быть от 0 до 10000 рублей")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Время начала урока обязательно")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Время окончания урока обязательно")]
        public DateTime EndTime { get; set; }

        [Required(ErrorMessage = "Длительность урока обязательна")]
        public int DurationMinutes { get; set; }

        [Required(ErrorMessage = "Предмет обязателен")]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "Учитель обязателен")]
        public int TutorId { get; set; }

        // Навигационные свойства
        public Subject Subject { get; set; }
        public User Tutor { get; set; }

        public virtual ICollection<Booking> Bookings { get; set; }
    }
} 