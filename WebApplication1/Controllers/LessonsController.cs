using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using System.Collections.Generic;

namespace WebApplication1.Controllers
{
    public class LessonsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LessonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Lessons/Create
        [Authorize(Roles = "Учитель")]
        public IActionResult Create()
        {
            var subjects = _context.Subjects.ToList();
            ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
            return View();
        }

        // POST: Lessons/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Учитель")]
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

        [Authorize(Roles = "Учитель")]
        public async Task<IActionResult> MyLessons()
        {
            var teacherEmail = User.FindFirstValue(ClaimTypes.Email);
            var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Email == teacherEmail);
            
            if (teacher == null)
            {
                return NotFound("Учитель не найден");
            }

            var lessons = await _context.Lessons
                .Include(l => l.Subject)
                .Include(l => l.Tutor)
                .Where(l => l.TutorId == teacher.Id)
                .OrderByDescending(l => l.StartTime)
                .ToListAsync();

            // Получаем количество бронирований для каждого урока
            var bookingsCount = new Dictionary<int, int>();
            foreach (var lesson in lessons)
            {
                var count = await _context.Bookings
                    .CountAsync(b => b.LessonId == lesson.Id);
                bookingsCount[lesson.Id] = count;
            }

            ViewBag.BookingsCount = bookingsCount;
            return View(lessons);
        }

        [Authorize(Roles = "Учитель")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var lesson = await _context.Lessons
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
            {
                return NotFound("Урок не найден");
            }

            var teacherEmail = User.FindFirstValue(ClaimTypes.Email);
            var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Email == teacherEmail);

            if (lesson.TutorId != teacher.Id)
            {
                return Forbid("Вы не можете удалить чужой урок");
            }

            // Проверяем наличие бронирований
            var hasBookings = await _context.Bookings.AnyAsync(b => b.LessonId == id);
            if (hasBookings)
            {
                TempData["Error"] = "Нельзя удалить урок, на который уже записались ученики";
                return RedirectToAction(nameof(MyLessons));
            }

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Урок успешно удален";
            return RedirectToAction(nameof(MyLessons));
        }

        [Authorize(Roles = "Ученик")]
        public async Task<IActionResult> Search(string searchString, int? subjectId, string sortOrder)
        {
            var query = _context.Lessons
                .Include(l => l.Subject)
                .Include(l => l.Tutor)
                .Where(l => l.StartTime >= DateTime.Now)
                .AsQueryable();

            // Фильтрация по поисковой строке
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(l => 
                    l.Title.Contains(searchString) || 
                    l.Description.Contains(searchString) ||
                    l.Subject.Name.Contains(searchString) ||
                    (l.Tutor.FirstName + " " + l.Tutor.LastName).Contains(searchString)
                );
            }

            // Фильтрация по предмету
            if (subjectId.HasValue)
            {
                query = query.Where(l => l.SubjectId == subjectId.Value);
            }

            // Сортировка
            switch (sortOrder)
            {
                case "date_desc":
                    query = query.OrderByDescending(l => l.StartTime);
                    break;
                case "price_asc":
                    query = query.OrderBy(l => l.Price);
                    break;
                case "price_desc":
                    query = query.OrderByDescending(l => l.Price);
                    break;
                default:
                    query = query.OrderBy(l => l.StartTime);
                    break;
            }

            var lessons = await query.ToListAsync();
            var subjects = await _context.Subjects.OrderBy(s => s.Name).ToListAsync();

            ViewBag.SearchString = searchString;
            ViewBag.SubjectId = subjectId;
            ViewBag.SortOrder = sortOrder;
            ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
            ViewBag.CurrentSort = sortOrder;

            return View(lessons);
        }

        [Authorize(Roles = "Ученик")]
        [HttpPost]
        public async Task<IActionResult> SignUp(int id)
        {
            // Получаем урок
            var lesson = await _context.Lessons
                .Include(l => l.Tutor)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
            {
                TempData["Error"] = "Урок не найден";
                return RedirectToAction(nameof(Search));
            }

            // Проверяем, не прошел ли уже урок
            if (lesson.StartTime < DateTime.Now)
            {
                TempData["Error"] = "Нельзя записаться на прошедший урок";
                return RedirectToAction(nameof(Search));
            }

            // Получаем текущего пользователя
            var studentEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Email == studentEmail);

            if (student == null)
            {
                TempData["Error"] = "Пользователь не найден";
                return RedirectToAction(nameof(Search));
            }

            // Проверяем, не является ли ученик учителем этого урока
            if (student.Id == lesson.TutorId)
            {
                TempData["Error"] = "Вы не можете записаться на свой урок";
                return RedirectToAction(nameof(Search));
            }

            // Проверяем, не записан ли уже ученик на этот урок
            var existingBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.LessonId == id && b.StudentId == student.Id);

            if (existingBooking != null)
            {
                TempData["Error"] = "Вы уже записаны на этот урок";
                return RedirectToAction(nameof(Search));
            }

            try
            {
                // Создаем новую запись
                var booking = new Booking
                {
                    LessonId = id,
                    StudentId = student.Id,
                    StatusId = 1 // Предполагаем, что 1 - это ID статуса "Ожидает подтверждения"
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Вы успешно записались на урок";
                return RedirectToAction(nameof(Search));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ошибка при записи на урок: " + ex.Message;
                return RedirectToAction(nameof(Search));
            }
        }

        [Authorize(Roles = "Ученик")]
        public async Task<IActionResult> MyBookings()
        {
            var studentEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Email == studentEmail);
            
            if (student == null)
            {
                return NotFound("Ученик не найден");
            }

            var bookings = await _context.Bookings
                .Include(b => b.Lesson)
                    .ThenInclude(l => l.Subject)
                .Include(b => b.Lesson)
                    .ThenInclude(l => l.Tutor)
                .Include(b => b.Status)
                .Where(b => b.StudentId == student.Id)
                .OrderByDescending(b => b.Lesson.StartTime)
                .ToListAsync();

            return View(bookings);
        }

        [Authorize(Roles = "Ученик")]
        [HttpPost]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var studentEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Email == studentEmail);
            
            if (student == null)
            {
                return NotFound("Ученик не найден");
            }

            var booking = await _context.Bookings
                .Include(b => b.Lesson)
                .FirstOrDefaultAsync(b => b.Id == id && b.StudentId == student.Id);

            if (booking == null)
            {
                return NotFound("Запись не найдена");
            }

            // Проверяем, не прошел ли уже урок
            if (booking.Lesson.StartTime < DateTime.Now)
            {
                TempData["Error"] = "Нельзя отменить запись на прошедший урок";
                return RedirectToAction(nameof(MyBookings));
            }

            try
            {
                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Запись успешно отменена";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ошибка при отмене записи: " + ex.Message;
            }

            return RedirectToAction(nameof(MyBookings));
        }
    }
} 