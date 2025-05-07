using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics;
using System.Threading.Tasks;
using BookNook.Data;
using BookNook.Models;

namespace BookNook.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var books = await _context.Books
                .OrderByDescending(b => b.AddedDate)
                .Take(6)
                .ToListAsync();

            var bookIds = books.Select(b => b.Id).ToList();
            var timedDiscounts = await _context.TimedDiscounts
                .Where(td => bookIds.Contains(td.BookId))
                .ToListAsync();

            var booksWithDiscounts = books.Select(b =>
            {
                var d = timedDiscounts.FirstOrDefault(td =>
                            td.BookId == b.Id &&
                            td.StartDate <= DateTime.UtcNow &&
                            td.ExpiresAt >= DateTime.UtcNow);

                return new BookWithDiscountViewModel
                {
                    Book = b,
                    OnSaleFlag = d?.OnSaleFlag ?? false,
                    IsDiscountActive = d != null,
                    DiscountedPrice = d != null ? b.Price * (1 - d.DiscountPercentage) : b.Price
                };
            }).ToList();

            ApplicationUser? currentUser = null;
            if (User.Identity?.IsAuthenticated == true)
                currentUser = await _userManager.GetUserAsync(User);

            var announcements = await _context.TimedAnnouncements
                .Where(a => a.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            var model = new HomeViewModel
            {
                Books = booksWithDiscounts,
                CurrentUser = currentUser,
                TimedAnnouncements = announcements
            };

            ViewData["CurrentUser"] = currentUser;
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
