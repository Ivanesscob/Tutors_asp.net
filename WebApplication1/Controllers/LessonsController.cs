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
        [Authorize(Roles = "Учитель")]
        public async Task<IActionResult> Create(string title, string description, decimal price, DateTime startTime, DateTime endTime, int subjectId)
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

        [Authorize(Roles = "Учитель")]
        public async Task<IActionResult> MyLessons()
        {
            var currentTime = DateTime.Now;
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            
            if (teacher == null)
            {
                return NotFound("Учитель не найден");
            }
            
            var lessons = await _context.Lessons
                .Include(l => l.Subject)
                .Include(l => l.Bookings)
                    .ThenInclude(b => b.Status)
                .Where(l => l.TutorId == teacher.Id)
                .OrderByDescending(l => l.StartTime)
                .ToListAsync();

            // Обновляем статусы записей
            foreach (var lesson in lessons)
            {
                foreach (var booking in lesson.Bookings)
                {
                    if (lesson.StartTime < currentTime && booking.Status.Name != "Отменено")
                    {
                        var completedStatus = await _context.BookingStatuses
                            .FirstOrDefaultAsync(s => s.Name == "Завершен");
                        
                        if (completedStatus != null && booking.StatusId != completedStatus.Id)
                        {
                            booking.StatusId = completedStatus.Id;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

           
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

           
            if (subjectId.HasValue)
            {
                query = query.Where(l => l.SubjectId == subjectId.Value);
            }

           
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

           
            if (User.Identity.IsAuthenticated)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user != null)
                {
                    var userBookings = await _context.Bookings
                        .Where(b => b.StudentId == user.Id)
                        .Select(b => b.LessonId)
                        .ToListAsync();
                    ViewBag.UserBookings = userBookings;
                }
            }

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
            
            var lesson = await _context.Lessons
                .Include(l => l.Tutor)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
            {
                TempData["Error"] = "Урок не найден";
                return RedirectToAction(nameof(Search));
            }

            
            if (lesson.StartTime < DateTime.Now)
            {
                TempData["Error"] = "Нельзя записаться на прошедший урок";
                return RedirectToAction(nameof(Search));
            }

            
            var studentEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Email == studentEmail);

            if (student == null)
            {
                TempData["Error"] = "Пользователь не найден";
                return RedirectToAction(nameof(Search));
            }

           
            if (student.Id == lesson.TutorId)
            {
                TempData["Error"] = "Вы не можете записаться на свой урок";
                return RedirectToAction(nameof(Search));
            }

           
            var existingBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.LessonId == id && b.StudentId == student.Id);

            if (existingBooking != null)
            {
                TempData["Error"] = "Вы уже записаны на этот урок";
                return RedirectToAction(nameof(Search));
            }

            try
            {
                
                var pendingStatus = await _context.BookingStatuses
                    .FirstOrDefaultAsync(s => s.Name == "Ожидается");

                if (pendingStatus == null)
                {
                    TempData["Error"] = "Ошибка: статус 'Ожидается' не найден";
                    return RedirectToAction(nameof(Search));
                }

                
                var booking = new Booking
                {
                    LessonId = id,
                    StudentId = student.Id,
                    StatusId = pendingStatus.Id
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
            var currentTime = DateTime.Now;
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            
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

            
            foreach (var booking in bookings)
            {
                if (booking.Lesson.StartTime < currentTime && booking.Status.Name != "Отменено")
                {
                    var completedStatus = await _context.BookingStatuses
                        .FirstOrDefaultAsync(s => s.Name == "Завершен");
                    
                    if (completedStatus != null && booking.StatusId != completedStatus.Id)
                    {
                        booking.StatusId = completedStatus.Id;
                    }
                }
            }

            await _context.SaveChangesAsync();
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

        [Authorize(Roles = "Админ")]
        public async Task<IActionResult> AdminPanel()
        {
            
            var lessons = await _context.Lessons
                .Include(l => l.Subject)
                .Include(l => l.Tutor)
                .OrderByDescending(l => l.StartTime)
                .ToListAsync();

           
            var bookings = await _context.Bookings
                .Include(b => b.Lesson)
                    .ThenInclude(l => l.Subject)
                .Include(b => b.Lesson)
                    .ThenInclude(l => l.Tutor)
                .Include(b => b.Student)
                .Include(b => b.Status)
                .OrderByDescending(b => b.Lesson.StartTime)
                .ToListAsync();

      
            var roles = await _context.UserRoles.ToListAsync();
            var subjects = await _context.Subjects.ToListAsync();

            // Получаем статистику
            var totalLessons = lessons.Count;
            var totalBookings = bookings.Count;
            var activeBookings = bookings.Count(b => b.Lesson.StartTime >= DateTime.Now);
            var totalTeachers = await _context.Users.CountAsync(u => u.Role.Name == "Учитель");
            var totalStudents = await _context.Users.CountAsync(u => u.Role.Name == "Ученик");

            ViewBag.TotalLessons = totalLessons;
            ViewBag.TotalBookings = totalBookings;
            ViewBag.ActiveBookings = activeBookings;
            ViewBag.TotalTeachers = totalTeachers;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.Roles = roles;
            ViewBag.Subjects = subjects;

            return View((lessons, bookings));
        }

        [Authorize(Roles = "Админ")]
        [HttpPost]
        public async Task<IActionResult> DeleteLesson(int id)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Bookings)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
            {
                return NotFound("Урок не найден");
            }

            try
            {
            
                _context.Bookings.RemoveRange(lesson.Bookings);
                
                _context.Lessons.Remove(lesson);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Урок успешно удален";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ошибка при удалении урока: " + ex.Message;
            }

            return RedirectToAction(nameof(AdminPanel));
        }

        [Authorize(Roles = "Админ")]
        [HttpPost]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Lesson)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound("Запись не найдена");
            }

            try
            {
                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Запись успешно удалена";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ошибка при удалении записи: " + ex.Message;
            }

            return RedirectToAction(nameof(AdminPanel));
        }

        [Authorize(Roles = "Админ")]
        [HttpPost]
        public async Task<IActionResult> AddRole(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                TempData["Error"] = "Название роли не может быть пустым";
                return RedirectToAction(nameof(AdminPanel));
            }

            try
            {
                var role = new UserRole { Name = name };
                _context.UserRoles.Add(role);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Роль успешно добавлена";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ошибка при добавлении роли: " + ex.Message;
            }

            return RedirectToAction(nameof(AdminPanel));
        }

        [Authorize(Roles = "Админ")]
        [HttpPost]
        public async Task<IActionResult> AddSubject(string name, string description)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
            {
                TempData["Error"] = "Название и описание предмета не могут быть пустыми";
                return RedirectToAction(nameof(AdminPanel));
            }

            try
            {
                var subject = new Subject { Name = name, Description = description };
                _context.Subjects.Add(subject);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Предмет успешно добавлен";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ошибка при добавлении предмета: " + ex.Message;
            }

            return RedirectToAction(nameof(AdminPanel));
        }

        public async Task<IActionResult> Index()
        {
            var query = _context.Lessons
                .Include(l => l.Subject)
                .Include(l => l.Tutor)
                .Where(l => l.StartTime >= DateTime.Now)
                .OrderBy(l => l.StartTime)
                .AsQueryable();

            var lessons = await query.ToListAsync();

        
            if (User.Identity.IsAuthenticated)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user != null)
                {
                    var userBookings = await _context.Bookings
                        .Where(b => b.StudentId == user.Id)
                        .Select(b => b.LessonId)
                        .ToListAsync();
                    ViewBag.UserBookings = userBookings;
                }
            }

            return View(lessons);
        }
    }
} 