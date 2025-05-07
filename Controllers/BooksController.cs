using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BookNook.Data;
using BookNook.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BookHive.Hubs;

namespace BookNook.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<BooksController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<OrderNotificationHub> _hubContext;

        public BooksController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<BooksController> logger,
            UserManager<ApplicationUser> userManager,
            IHubContext<OrderNotificationHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 12,
            string search = "",
            string sort = "title",
            string category = "all",
            string author = "",
            string genre = "",
            string availability = "all",
            bool? physicalLibraryAccess = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            double? minRating = null,
            string language = "",
            string format = "",
            string publisher = "",
            string isbn = "")
        {
            var query = _context.Books.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(b =>
                    (b.Title != null && b.Title.ToLower().Contains(s)) ||
                    (b.ISBN != null && b.ISBN.ToLower().Contains(s)) ||
                    (b.Description != null && b.Description.ToLower().Contains(s)));
            }

            if (!string.IsNullOrEmpty(author))
                query = query.Where(b => b.Author != null && b.Author.ToLower().Contains(author.ToLower()));

            if (!string.IsNullOrEmpty(genre))
                query = query.Where(b => b.Genre != null && b.Genre.ToLower() == genre.ToLower());

            if (availability == "available")
                query = query.Where(b => b.Quantity > 0);
            else if (availability == "unavailable")
                query = query.Where(b => b.Quantity <= 0);

            if (physicalLibraryAccess.HasValue)
                query = query.Where(b => b.IsPhysicalLibraryAccess == physicalLibraryAccess);

            if (minPrice.HasValue) query = query.Where(b => b.Price >= minPrice);
            if (maxPrice.HasValue) query = query.Where(b => b.Price <= maxPrice);

            if (minRating.HasValue)
                query = query.Where(b => b.Reviews.Any()
                                         ? b.Reviews.Where(r => r.ParentReviewId == null).Average(r => r.Rating) >= minRating
                                         : false);

            if (!string.IsNullOrEmpty(language))
                query = query.Where(b => b.Language != null && b.Language.ToLower() == language.ToLower());

            if (!string.IsNullOrEmpty(format))
                query = query.Where(b => b.Format != null && b.Format.ToLower() == format.ToLower());

            if (!string.IsNullOrEmpty(publisher))
                query = query.Where(b => b.Publisher != null && b.Publisher.ToLower().Contains(publisher.ToLower()));

            switch (category.ToLower())
            {
                case "bestsellers": query = query.Where(b => b.IsBestseller); break;
                case "awardwinners": query = query.Where(b => b.IsAwardWinner); break;
                case "newreleases": query = query.Where(b => b.PublicationDate >= DateTime.UtcNow.AddMonths(-3)); break;
                case "newarrivals": query = query.Where(b => b.AddedDate >= DateTime.UtcNow.AddMonths(-1)); break;
                case "comingsoon": query = query.Where(b => b.PublicationDate > DateTime.UtcNow); break;
                case "deals":
                    query = query.Where(b => _context.TimedDiscounts.Any(td =>
                                   td.BookId == b.Id &&
                                   td.StartDate <= DateTime.UtcNow &&
                                   td.ExpiresAt >= DateTime.UtcNow));
                    break;
            }

            switch (sort.ToLower())
            {
                case "author": query = query.OrderBy(b => b.Author ?? string.Empty); break;
                case "publicationdate": query = query.OrderByDescending(b => b.PublicationDate); break;
                case "price": query = query.OrderBy(b => b.Price); break;
                case "popularity":
                    query = query.GroupJoin(
                                _context.Orders.Where(o => !o.IsCancelled),
                                b => b.Id,
                                o => o.BookId,
                                (b, orders) => new { b, sold = orders.Sum(o => o.Quantity) })
                             .OrderByDescending(x => x.sold)
                             .Select(x => x.b);
                    break;
                default:
                    query = query.OrderBy(b => b.Title ?? string.Empty); break;
            }

            var totalBooks = await query.CountAsync();
            var books = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var discounts = await _context.TimedDiscounts
                               .Where(td => books.Select(b => b.Id).Contains(td.BookId))
                               .ToListAsync();

            var list = books.Select(b =>
            {
                var d = discounts.FirstOrDefault(td =>
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

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalBooks / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.Sort = sort;
            ViewBag.Category = category;
            ViewBag.Author = author;
            ViewBag.Genre = genre;
            ViewBag.Availability = availability;
            ViewBag.PhysicalLibraryAccess = physicalLibraryAccess;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.MinRating = minRating;
            ViewBag.Language = language;
            ViewBag.Format = format;
            ViewBag.Publisher = publisher;
            ViewBag.ISBN = isbn;

            if (User.Identity.IsAuthenticated)
            {
                var u = await _userManager.GetUserAsync(User);
                ViewBag.HasCartItems = u != null && await _context.Carts.AnyAsync(c => c.UserId == u.Id);
            }
            else ViewBag.HasCartItems = false;

            return View(list);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var book = await _context.Books
                .Include(b => b.Reviews).ThenInclude(r => r.User)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (book == null) return NotFound();

            var discount = await _context.TimedDiscounts
                .FirstOrDefaultAsync(td =>
                    td.BookId == id &&
                    td.StartDate <= DateTime.UtcNow &&
                    td.ExpiresAt >= DateTime.UtcNow);

            var reviews = await _context.Reviews
                .Where(r => r.BookId == id && r.ParentReviewId == null)
                .Include(r => r.User)
                .Include(r => r.Replies)
                .ThenInclude(rp => rp.User)
                .ToListAsync();

            var model = new BookWithDiscountViewModel
            {
                Book = book,
                OnSaleFlag = discount?.OnSaleFlag ?? false,
                IsDiscountActive = discount != null,
                DiscountedPrice = discount != null ? book.Price * (1 - discount.DiscountPercentage) : book.Price,
                Reviews = reviews
            };

            var mostRated = await _context.Books
                .Select(b => new
                {
                    b,
                    rating = b.Reviews.Any(r => r.ParentReviewId == null)
                                ? b.Reviews.Where(r => r.ParentReviewId == null).Average(r => r.Rating) : 0
                })
                .Where(x => x.b.Id != id)
                .OrderByDescending(x => x.rating)
                .Take(3)
                .Select(x => new BookWithDiscountViewModel
                {
                    Book = x.b,
                    OnSaleFlag = false,
                    IsDiscountActive = false,
                    DiscountedPrice = x.b.Price
                }).ToListAsync();

            var mostOrdered = await _context.Orders
                .Where(o => !o.IsCancelled)
                .GroupBy(o => o.BookId)
                .Select(g => new { g.Key, qty = g.Sum(o => o.Quantity) })
                .OrderByDescending(g => g.qty)
                .Take(3)
                .Join(_context.Books,
                      g => g.Key,
                      b => b.Id,
                      (g, b) => new BookWithDiscountViewModel
                      {
                          Book = b,
                          OnSaleFlag = false,
                          IsDiscountActive = false,
                          DiscountedPrice = b.Price
                      })
                .Where(b => b.Book.Id != id)
                .ToListAsync();

            ViewBag.MostRatedBooks = mostRated;
            ViewBag.MostOrderedBooks = mostOrdered;

            if (User.Identity.IsAuthenticated)
            {
                var u = await _userManager.GetUserAsync(User);
                ViewBag.HasPurchased = u != null && await _context.Orders.AnyAsync(o => o.UserId == u.Id && o.BookId == id && !o.IsCancelled);
                ViewBag.HasCartItems = u != null && await _context.Carts.AnyAsync(c => c.UserId == u.Id);
            }
            else
            {
                ViewBag.HasPurchased = false;
                ViewBag.HasCartItems = false;
            }

            return View(model);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetBookStock(int bookId)
        {
            var b = await _context.Books.FindAsync(bookId);
            return Json(new { stock = b?.Quantity ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AddToCart(int id, int quantity = 1)
        {
            _logger.LogInformation("Adding book {Id} to cart", id);
            var u = await _userManager.GetUserAsync(User);
            if (u == null) return Json(new { success = false, message = "User not found." });

            var b = await _context.Books.FindAsync(id);
            if (b == null) return Json(new { success = false, message = "Book not found." });
            if (!b.IsAvailable) return Json(new { success = false, message = "Out of stock." });
            if (quantity < 1) quantity = 1;

            var entry = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == u.Id && c.BookId == id);
            if (entry != null)
            {
                var newQty = entry.Quantity + quantity;
                if (newQty > b.Quantity) return Json(new { success = false, message = $"Only {b.Quantity} copies available." });
                entry.Quantity = newQty;
            }
            else
            {
                if (quantity > b.Quantity) return Json(new { success = false, message = $"Only {b.Quantity} copies available." });
                _context.Carts.Add(new Cart { UserId = u.Id, BookId = id, Quantity = quantity });
            }

            await _context.SaveChangesAsync();

            var count = await _context.Carts.Where(c => c.UserId == u.Id).Select(c => c.BookId).Distinct().CountAsync();
            await _hubContext.Clients.All.SendAsync("UpdateCartCount", count);

            return Json(new { success = true, message = "Added to cart." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ToggleWishlist(int id)
        {
            try
            {
                var u = await _userManager.GetUserAsync(User);
                if (u == null) return Json(new { success = false, message = "User not found." });

                var b = await _context.Books.FindAsync(id);
                if (b == null) return Json(new { success = false, message = "Book not found." });

                var w = await _context.Whitelists.FirstOrDefaultAsync(x => x.UserId == u.Id && x.BookId == id);
                if (w != null)
                {
                    _context.Whitelists.Remove(w);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, inWishlist = false, message = "Removed from whitelist." });
                }
                _context.Whitelists.Add(new Whitelist { UserId = u.Id, BookId = id });
                await _context.SaveChangesAsync();
                return Json(new { success = true, inWishlist = true, message = "Added to whitelist." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toggle wishlist error");
                return Json(new { success = false, message = "Server error." });
            }
        }

        [Authorize]
        public async Task<IActionResult> IsInWishlist(int bookId)
        {
            var u = await _userManager.GetUserAsync(User);
            if (u == null) return Json(new { inWishlist = false });
            var flag = await _context.Whitelists.AnyAsync(w => w.UserId == u.Id && w.BookId == bookId);
            return Json(new { inWishlist = flag });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create() => View(new BookViewModel());

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookViewModel m)
        {
            try
            {
                var b = new Book
                {
                    Title = m.Title ?? "",
                    Author = m.Author ?? "",
                    Description = m.Description ?? "",
                    AddedDate = DateTime.UtcNow,
                    Price = m.Price,
                    Quantity = m.Quantity
                };

                if (m.CoverImage != null && m.CoverImage.Length > 0)
                {
                    var folder = Path.Combine(_webHostEnvironment.WebRootPath, "images/book-covers");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    var name = $"{Guid.NewGuid()}_{Path.GetFileName(m.CoverImage.FileName)}";
                    var path = Path.Combine(folder, name);
                    await using var fs = new FileStream(path, FileMode.Create);
                    await m.CoverImage.CopyToAsync(fs);
                    b.CoverImageUrl = "/images/book-covers/" + name;
                }

                _context.Add(b);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Book '{b.Title}' created.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create book error");
                TempData["ErrorMessage"] = "Create failed.";
                return View(m);
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var b = await _context.Books.FindAsync(id);
            if (b == null) return NotFound();

            var vm = new BookViewModel
            {
                Id = b.Id,
                Title = b.Title,
                Author = b.Author,
                Description = b.Description,
                CoverImageUrl = b.CoverImageUrl,
                Price = b.Price,
                Quantity = b.Quantity
            };
            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BookViewModel m)
        {
            if (id != m.Id) return NotFound();
            if (!ModelState.IsValid) return View(m);

            try
            {
                var b = await _context.Books.FindAsync(id);
                if (b == null) return NotFound();

                var old = b.CoverImageUrl;
                b.Title = m.Title ?? b.Title;
                b.Author = m.Author ?? b.Author;
                b.Description = m.Description ?? b.Description;
                b.Price = m.Price;
                b.Quantity = m.Quantity;

                if (m.CoverImage != null && m.CoverImage.Length > 0)
                {
                    var folder = Path.Combine(_webHostEnvironment.WebRootPath, "images/book-covers");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    var name = $"{Guid.NewGuid()}_{Path.GetFileName(m.CoverImage.FileName)}";
                    var path = Path.Combine(folder, name);
                    await using var fs = new FileStream(path, FileMode.Create);
                    await m.CoverImage.CopyToAsync(fs);
                    b.CoverImageUrl = "/images/book-covers/" + name;

                    if (!string.IsNullOrEmpty(old))
                    {
                        var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, old.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                    }
                }

                _context.Update(b);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Book '{b.Title}' updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Books.Any(e => e.Id == m.Id)) return NotFound();
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit book error");
                TempData["ErrorMessage"] = "Edit failed.";
                return View(m);
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var b = await _context.Books.FindAsync(id);
            if (b == null) return NotFound();
            return View(b);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var b = await _context.Books.FindAsync(id);
                if (b != null)
                {
                    if (!string.IsNullOrEmpty(b.CoverImageUrl))
                    {
                        var p = Path.Combine(_webHostEnvironment.WebRootPath, b.CoverImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
                    }
                    _context.Books.Remove(b);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Book '{b.Title}' deleted.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete book error");
                TempData["ErrorMessage"] = "Delete failed.";
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ManageDiscounts(int id)
        {
            var b = await _context.Books.FindAsync(id);
            if (b == null) return NotFound();

            var d = await _context.TimedDiscounts.FirstOrDefaultAsync(td => td.BookId == id);
            var vm = new TimedDiscountViewModel
            {
                BookId = b.Id,
                BookTitle = b.Title,
                DiscountPercentage = d?.DiscountPercentage * 100 ?? 0,
                StartDate = d?.StartDate ?? DateTime.UtcNow,
                ExpiresAt = d?.ExpiresAt ?? DateTime.UtcNow.AddDays(7),
                OnSaleFlag = d?.OnSaleFlag ?? false
            };
            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageDiscounts(TimedDiscountViewModel m)
        {
            if (!ModelState.IsValid) return View(m);
            var b = await _context.Books.FindAsync(m.BookId);
            if (b == null) return NotFound();

            var d = await _context.TimedDiscounts.FirstOrDefaultAsync(td => td.BookId == m.BookId);

            if (m.DiscountPercentage > 0)
            {
                if (d == null)
                    _context.TimedDiscounts.Add(new TimedDiscount
                    {
                        BookId = m.BookId,
                        DiscountPercentage = m.DiscountPercentage / 100,
                        StartDate = m.StartDate,
                        ExpiresAt = m.ExpiresAt,
                        OnSaleFlag = m.OnSaleFlag
                    });
                else
                {
                    d.DiscountPercentage = m.DiscountPercentage / 100;
                    d.StartDate = m.StartDate;
                    d.ExpiresAt = m.ExpiresAt;
                    d.OnSaleFlag = m.OnSaleFlag;
                    _context.TimedDiscounts.Update(d);
                }
            }
            else if (d != null) _context.TimedDiscounts.Remove(d);

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Discount updated.";
            return RedirectToAction("Details", new { id = m.BookId });
        }
    }
}
