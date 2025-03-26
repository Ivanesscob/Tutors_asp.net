using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class Subject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название предмета обязательно")]
        [Display(Name = "Название")]
        public string Name { get; set; }

        [Display(Name = "Описание")]
        public string Description { get; set; }
    }
} 