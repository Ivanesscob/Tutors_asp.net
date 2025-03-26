using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Учитель")]
    public class LessonsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LessonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Lessons/Create
        public IActionResult Create()
        {
            var subjects = _context.Subjects.ToList();
            ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
            return View();
        }

        // POST: Lessons/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, string description, decimal price, DateTime startTime, DateTime endTime, int subjectId)
        {
            // Проверяем, существует ли урок с таким названием
            var existingLesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Title == title);
            if (existingLesson != null)
            {
                ModelState.AddModelError("Title", "Урок с таким названием уже существует");
                ViewBag.Subjects = new SelectList(_context.Subjects, "Id", "Name");
                return View();
            }

            // Логируем все Claims пользователя
            foreach (var claim in User.Claims)
            {
                System.Diagnostics.Debug.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
            }

            // Логируем полученные данные
            System.Diagnostics.Debug.WriteLine($"SubjectId: {subjectId}");
            System.Diagnostics.Debug.WriteLine($"User.Identity.Name: {User.Identity.Name}");

            try
            {
                // Получаем email пользователя из Claims
                var emailClaim = User.FindFirst(ClaimTypes.Email);
                if (emailClaim == null)
                {
                    ModelState.AddModelError("", "Не удалось определить email пользователя");
                    ViewBag.Subjects = new SelectList(_context.Subjects, "Id", "Name");
                    return View();
                }

                // Получаем пользователя по email
                var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Email == emailClaim.Value);
                if (teacher == null)
                {
                    ModelState.AddModelError("", "Учитель не найден в базе данных");
                    ViewBag.Subjects = new SelectList(_context.Subjects, "Id", "Name");
                    return View();
                }

                System.Diagnostics.Debug.WriteLine($"Found TeacherId: {teacher.Id}");

                var lesson = new Lesson
                {
                    Title = title,
                    Description = description,
                    Price = price,
                    StartTime = startTime,
                    EndTime = endTime,
                    SubjectId = subjectId,
                    TutorId = teacher.Id,
                    DurationMinutes = (int)(endTime - startTime).TotalMinutes
                };
                
                _context.Add(lesson);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
                ModelState.AddModelError("", "Ошибка при сохранении урока: " + ex.Message);
            }

            ViewBag.Subjects = new SelectList(_context.Subjects, "Id", "Name");
            return View();
        }
    }
} 