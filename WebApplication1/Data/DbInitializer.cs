using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            // Проверяем, есть ли уже роли
            if (context.UserRoles.Any())
            {
                return; // База данных уже инициализирована
            }

            // Добавляем роли
            var roles = new UserRole[]
            {
                new UserRole { Name = "Учитель" },
                new UserRole { Name = "Ученик" }
            };

            foreach (var role in roles)
            {
                context.UserRoles.Add(role);
            }

            context.SaveChanges();
        }
    }
} 