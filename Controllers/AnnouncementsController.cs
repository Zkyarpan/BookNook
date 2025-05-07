using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading.Tasks;

using BookNook.Data;       
using BookNook.Models;     

namespace BookNook.Controllers
{
    [Authorize]
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementsController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /* ------------------------------------------------ INDEX -------- */
        public async Task<IActionResult> Index()
        {
            var list = await _context.TimedAnnouncements
                                     .Where(a => a.ExpiresAt >= DateTime.UtcNow)
                                     .ToListAsync();
            return View(list);
        }

        /* ------------------------------------------------ CREATE ------- */
        [Authorize(Roles = "Admin")]
        public IActionResult Create() =>
            View(new TimedAnnouncement
            {
                StartDate = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            });

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TimedAnnouncement m)
        {
            if (!ModelState.IsValid) return View(m);

            m.CreatedAt = DateTime.UtcNow;
            _context.TimedAnnouncements.Add(m);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Announcement created successfully.";
            return RedirectToAction(nameof(Index));
        }

        /* ------------------------------------------------ EDIT --------- */
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var a = await _context.TimedAnnouncements.FindAsync(id);
            return a == null ? NotFound() : View(a);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TimedAnnouncement m)
        {
            if (id != m.Id) return NotFound();
            if (!ModelState.IsValid) return View(m);

            var a = await _context.TimedAnnouncements.FindAsync(id);
            if (a == null) return NotFound();

            a.Title = m.Title;
            a.Message = m.Message;
            a.StartDate = m.StartDate;
            a.ExpiresAt = m.ExpiresAt;

            _context.Update(a);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Announcement updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        /* ------------------------------------------------ DELETE ------- */
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _context.TimedAnnouncements.FindAsync(id);
            return a == null ? NotFound() : View(a);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var a = await _context.TimedAnnouncements.FindAsync(id);
            if (a != null)
            {
                _context.TimedAnnouncements.Remove(a);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Announcement deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
