using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class Lesson
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название урока обязательно")]
        [Display(Name = "Название")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Описание обязательно")]
        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Цена обязательна")]
        [Display(Name = "Цена")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Длительность обязательна")]
        [Display(Name = "Длительность (минут)")]
        public int DurationMinutes { get; set; }

        [Required(ErrorMessage = "Начало урока обязательно")]
        [Display(Name = "Начало урока")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm}", ApplyFormatInEditMode = true)]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Конец урока обязателен")]
        [Display(Name = "Конец урока")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm}", ApplyFormatInEditMode = true)]
        public DateTime EndTime { get; set; }

        [Required(ErrorMessage = "Репетитор обязателен")]
        [Display(Name = "Репетитор")]
        public int TutorId { get; set; }
        public virtual User Tutor { get; set; }

        [Required(ErrorMessage = "Предмет обязателен")]
        [Display(Name = "Предмет")]
        public int SubjectId { get; set; }
        public virtual Subject Subject { get; set; }
    }
} 