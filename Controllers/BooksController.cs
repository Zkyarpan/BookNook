using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookNook.Data;
using BookNook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using BookNook.Hubs;


namespace BookNook.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<BooksController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<OrderNotificationHub> _hubContext;

        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB in bytes
        private readonly string[] AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };

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

        // Helper method to log errors and set TempData
        private void HandleError(string message, Exception ex = null, string title = null)
        {
            if (ex != null)
            {
                if (title != null)
                {
                    _logger.LogError(ex, "{Message} for book {BookTitle}", message, title);
                }
                else
                {
                    _logger.LogError(ex, "{Message}", message);
                }
                message = $"{message}: {ex.Message}";
            }
            else
            {
                if (title != null)
                {
                    _logger.LogWarning("{Message} for book {BookTitle}", message, title);
                }
                else
                {
                    _logger.LogWarning("{Message}", message);
                }
            }
            TempData["ErrorMessage"] = message;
        }

        // Helper method to get the current user
        private async Task<ApplicationUser> GetCurrentUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found");
            }
            return user;
        }

        // Helper method to check if the user has cart items
        private async Task<bool> HasCartItemsAsync(ApplicationUser user)
        {
            if (user == null) return false;
            return await _context.Carts.AnyAsync(c => c.UserId == user.Id);
        }

        // Helper method to handle file uploads
        private async Task<string> HandleFileUploadAsync(IFormFile file, string uploadsFolder, string oldFilePath = null)
        {
            // Validate file type
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!AllowedExtensions.Contains(fileExtension))
            {
                throw new Exception("Only JPG, JPEG, PNG, and GIF files are allowed.");
            }

            // Validate file size
            if (file.Length > MaxFileSize)
            {
                throw new Exception("The cover image file size must not exceed 5MB.");
            }

            // Create uploads directory if it doesn't exist
            if (!Directory.Exists(uploadsFolder))
            {
                try
                {
                    Directory.CreateDirectory(uploadsFolder);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to create upload directory", ex);
                }
            }

            // Save the new file
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save cover image", ex);
            }

            // Delete the old file if it exists
            if (!string.IsNullOrEmpty(oldFilePath))
            {
                var oldFullPath = Path.Combine(_webHostEnvironment.WebRootPath, oldFilePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFullPath))
                {
                    System.IO.File.Delete(oldFullPath);
                }
            }

            return "/images/book-covers/" + uniqueFileName;
        }

        // GET: Books/Index
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 6,
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
            bool? isComingSoon = null,
            string isbn = "")
        {
            var query = _context.Books.AsQueryable();
            var booksQuery = _context.Books.Include(b => b.Reviews).AsQueryable();
            // Search by title, ISBN, or description
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(b =>
                    (b.Title != null && b.Title.ToLower().Contains(search)) ||
                    (b.ISBN != null && b.ISBN.ToLower().Contains(search)) ||
                    (b.Description != null && b.Description.ToLower().Contains(search)));
            }

            // Filter by author
            if (!string.IsNullOrEmpty(author))
            {
                query = query.Where(b => b.Author != null && b.Author.ToLower().Contains(author.ToLower()));
            }

            // Filter by genre
            if (!string.IsNullOrEmpty(genre))
            {
                query = query.Where(b => b.Genre != null && b.Genre.ToLower() == genre.ToLower());
            }

            // Filter by availability (stock)
            if (availability == "available")
            {
                query = query.Where(b => b.Quantity > 0);
            }
            else if (availability == "unavailable")
            {
                query = query.Where(b => b.Quantity <= 0);
            }

            // Filter by physical library access
            if (physicalLibraryAccess.HasValue)
            {
                query = query.Where(b => b.IsPhysicalLibraryAccess == physicalLibraryAccess.Value);
            }

            // Filter by price range
            if (minPrice.HasValue)
            {
                query = query.Where(b => b.Price >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(b => b.Price <= maxPrice.Value);
            }

            // Filter by ratings
            if (minRating.HasValue)
            {
                query = query.Where(b => b.Reviews.Any() ? b.Reviews.Where(r => r.ParentReviewId == null).Average(r => r.Rating) >= minRating.Value : false);
            }

            // Filter by language
            if (!string.IsNullOrEmpty(language))
            {
                query = query.Where(b => b.Language != null && b.Language.ToLower() == language.ToLower());
            }

            // Filter by format
            if (!string.IsNullOrEmpty(format))
            {
                query = query.Where(b => b.Format != null && b.Format.ToLower().Equals(format, StringComparison.CurrentCultureIgnoreCase));
            }

            // Filter by publisher
            if (!string.IsNullOrEmpty(publisher))
            {
                query = query.Where(b => b.Publisher != null && b.Publisher.ToLower().Contains(publisher.ToLower()));
            }

            // Apply "Coming Soon" filter from the sidebar
            DateTime currentDate = DateTime.SpecifyKind(new DateTime(2025, 5, 13), DateTimeKind.Utc);
            if (isComingSoon.HasValue)
            {
                if (isComingSoon.Value)
                {
                    // Show only books that are coming soon and not yet released
                    query = query.Where(b => b.IsComingSoon && b.ReleaseDate.HasValue && b.ReleaseDate.Value > currentDate);
                }
                else
                {
                    // Show only books that are released or not marked as coming soon
                    query = query.Where(b => !b.IsComingSoon || !b.ReleaseDate.HasValue || b.ReleaseDate.Value <= currentDate);
                }
            }

            // Category tabs
            switch (category.ToLower())
            {
                case "bestsellers":
                    query = query.Where(b => b.IsBestseller);
                    break;
                case "awardwinners":
                    query = query.Where(b => b.IsAwardWinner);
                    break;
                case "newreleases":
                    var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
                    query = query.Where(b => b.PublicationDate >= threeMonthsAgo);
                    break;
                case "newarrivals":
                    var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                    query = query.Where(b => b.AddedDate >= oneMonthAgo);
                    break;
                case "comingsoon":
                    // Updated to use IsComingSoon and ReleaseDate instead of PublicationDate
                    query = query.Where(b => b.IsComingSoon && b.ReleaseDate.HasValue && b.ReleaseDate.Value > currentDate);
                    break;
                case "deals":
                    query = query.Where(b => _context.TimedDiscounts.Any(td => td.BookId == b.Id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow));
                    break;
                case "all":
                default:
                    break;
            }

            // Sorting
            switch (sort.ToLower())
            {
                case "author":
                    query = query.OrderBy(b => b.Author ?? string.Empty);
                    break;
                case "publicationdate":
                    query = query.OrderByDescending(b => b.PublicationDate);
                    break;
                case "price":
                    query = query.OrderBy(b => b.Price);
                    break;
                case "popularity":
                    query = query
                        .GroupJoin(_context.Orders.Where(o => !o.IsCancelled),
                            b => b.Id,
                            o => o.BookId,
                            (b, orders) => new { Book = b, TotalSold = orders.Sum(o => o.Quantity) })
                        .OrderByDescending(x => x.TotalSold)
                        .Select(x => x.Book);
                    break;
                case "title":
                default:
                    query = query.OrderBy(b => b.Title ?? string.Empty);
                    break;
            }

            // Pagination
            var totalBooks = await query.CountAsync();
            var books = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Apply discounts
            var bookIds = books.Select(b => b.Id).ToList();
            var timedDiscounts = await _context.TimedDiscounts
                .Where(td => bookIds.Contains(td.BookId))
                .ToListAsync();

            var booksWithDiscounts = books.Select(book =>
            {
                var discount = timedDiscounts.FirstOrDefault(td => td.BookId == book.Id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                return new BookWithDiscountViewModel
                {
                    Book = book,
                    OnSaleFlag = discount?.OnSaleFlag ?? false,
                    IsDiscountActive = discount != null,
                    DiscountedPrice = discount != null ? book.Price * (1 - discount.DiscountPercentage) : book.Price
                };
            }).ToList();

            // Set ViewBag properties for the view
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalBooks / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
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
            ViewBag.IsComingSoon = isComingSoon;

            // Handle cart items for authenticated users
            var user = await GetCurrentUserAsync();
            ViewBag.HasCartItems = await HasCartItemsAsync(user);

            return View(booksWithDiscounts);
        }

        // GET: Books/Details/{id}
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var book = await _context.Books
                .Include(b => b.Reviews)
                .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            var discount = await _context.TimedDiscounts
                .FirstOrDefaultAsync(td => td.BookId == id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);

            var reviews = await _context.Reviews
                .Where(r => r.BookId == id && r.ParentReviewId == null)
                .Include(r => r.User)
                .Include(r => r.Replies)
                .ThenInclude(reply => reply.User)
                .ToListAsync();

            var model = new BookWithDiscountViewModel
            {
                Book = book,
                OnSaleFlag = discount?.OnSaleFlag ?? false,
                IsDiscountActive = discount != null,
                DiscountedPrice = discount != null ? book.Price * (1 - discount.DiscountPercentage) : book.Price,
                Reviews = reviews
            };

            var mostRatedBooks = await _context.Books
                .Select(b => new
                {
                    Book = b,
                    AverageRating = b.Reviews.Any(r => r.ParentReviewId == null) ? b.Reviews.Where(r => r.ParentReviewId == null).Average(r => r.Rating) : 0
                })
                .Where(b => b.Book.Id != book.Id)
                .OrderByDescending(b => b.AverageRating)
                .Take(3)
                .Select(b => new BookWithDiscountViewModel
                {
                    Book = b.Book,
                    OnSaleFlag = false,
                    IsDiscountActive = false,
                    DiscountedPrice = b.Book.Price
                })
                .ToListAsync();

            var mostOrderedBooks = await _context.Orders
                .Where(o => !o.IsCancelled)
                .GroupBy(o => o.BookId)
                .Select(g => new
                {
                    BookId = g.Key,
                    TotalQuantity = g.Sum(o => o.Quantity)
                })
                .OrderByDescending(g => g.TotalQuantity)
                .Take(3)
                .Join(_context.Books,
                    g => g.BookId,
                    b => b.Id,
                    (g, b) => new BookWithDiscountViewModel
                    {
                        Book = b,
                        OnSaleFlag = false,
                        IsDiscountActive = false,
                        DiscountedPrice = b.Price
                    })
                .Where(b => b.Book.Id != book.Id)
                .ToListAsync();

            ViewBag.MostRatedBooks = mostRatedBooks;
            ViewBag.MostOrderedBooks = mostOrderedBooks;

            var user = await GetCurrentUserAsync();
            ViewBag.HasPurchased = user != null && await _context.Orders
                .AnyAsync(o => o.UserId == user.Id && o.BookId == id && !o.IsCancelled);
            ViewBag.HasCartItems = await HasCartItemsAsync(user);

            return View(model);
        }

        // GET: Books/GetBookStock?bookId={id}
        [AllowAnonymous]
        public async Task<IActionResult> GetBookStock(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            return Json(new { stock = book?.Quantity ?? 0 });
        }

        // POST: Books/AddToCart/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AddToCart(int id, int quantity = 1)
        {
            _logger.LogInformation("Attempting to add book {BookId} to cart for current user", id);

            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return Json(new { success = false, message = "User not found. Please log in." });
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                _logger.LogWarning("Book not found in AddToCart for id {BookId}", id);
                return Json(new { success = false, message = "Book not found." });
            }

            if (!book.IsAvailable)
            {
                return Json(new { success = false, message = "This book is currently out of stock." });
            }

            if (quantity < 1)
            {
                quantity = 1;
            }

            var existingEntry = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == id);
            if (existingEntry != null)
            {
                var newQuantity = existingEntry.Quantity + quantity;
                if (newQuantity > book.Quantity)
                {
                    return Json(new { success = false, message = $"Only {book.Quantity} copies of '{book.Title}' are available." });
                }
                existingEntry.Quantity = newQuantity;
            }
            else
            {
                if (quantity > book.Quantity)
                {
                    return Json(new { success = false, message = $"Only {book.Quantity} copies of '{book.Title}' are available." });
                }
                var cartEntry = new Cart
                {
                    UserId = user.Id,
                    BookId = id,
                    Quantity = quantity
                };
                _context.Carts.Add(cartEntry);
            }

            await _context.SaveChangesAsync();

            // Broadcast updated cart count to all clients
            int cartCount = await _context.Carts
                .Where(c => c.UserId == user.Id)
                .Select(c => c.BookId)
                .Distinct()
                .CountAsync();
            await _hubContext.Clients.All.SendAsync("UpdateCartCount", cartCount);

            return Json(new { success = true, message = "Cart was added successfully!" });
        }

        // POST: Books/ToggleWishlist/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ToggleWishlist(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to toggle whitelist status for book {BookId} for current user", id);

                var user = await GetCurrentUserAsync();
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    return Json(new { success = false, message = "Book not found." });
                }

                var existingEntry = await _context.Whitelists
                    .FirstOrDefaultAsync(w => w.UserId == user.Id && w.BookId == id);

                if (existingEntry != null)
                {
                    // Remove from whitelist
                    _context.Whitelists.Remove(existingEntry);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, inWishlist = false, message = "Book removed from your whitelist." });
                }
                else
                {
                    // Add to whitelist
                    var whitelistEntry = new Whitelist
                    {
                        UserId = user.Id,
                        BookId = id
                    };
                    _context.Whitelists.Add(whitelistEntry);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, inWishlist = true, message = "Book added to your whitelist." });
                }
            }
            catch (Exception ex)
            {
                HandleError("Error toggling whitelist for book", ex, id.ToString());
                return Json(new { success = false, message = TempData["ErrorMessage"]?.ToString() });
            }
        }

        // GET: Books/IsInWishlist?bookId={id}
        [Authorize]
        public async Task<IActionResult> IsInWishlist(int bookId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return Json(new { inWishlist = false });
            }

            var inWishlist = await _context.Whitelists
                .AnyAsync(w => w.UserId == user.Id && w.BookId == bookId);

            return Json(new { inWishlist = inWishlist });
        }

        // GET: Books/Create
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new BookViewModel());
        }

        // POST: Books/Create
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookViewModel model)
        {
            try
            {
                var book = new Book
                {
                    Title = string.IsNullOrWhiteSpace(model.Title) ? "Untitled" : model.Title,
                    Author = string.IsNullOrWhiteSpace(model.Author) ? "Unknown Author" : model.Author,
                    Genre = model.Genre,
                    Description = model.Description,
                    ISBN = model.ISBN,
                    Language = model.Language,
                    Format = model.Format,
                    Publisher = model.Publisher,
                    Price = model.Price,
                    Quantity = model.Quantity,
                    IsPhysicalLibraryAccess = model.IsPhysicalLibraryAccess,
                    IsBestseller = model.IsBestseller,
                    IsAwardWinner = model.IsAwardWinner,
                    IsComingSoon = model.IsComingSoon,
                    AddedDate = DateTime.UtcNow,
                    PublicationDate = (model.PublicationDate ?? DateTime.UtcNow).ToUniversalTime(),
                    ReleaseDate = model.IsComingSoon && model.ReleaseDate.HasValue ? model.ReleaseDate.Value.ToUniversalTime() : null // Set to null if IsComingSoon is false
                };

                if (model.CoverImage != null && model.CoverImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images/book-covers");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.CoverImage.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.CoverImage.CopyToAsync(fileStream);
                    }

                    book.CoverImageUrl = "/images/book-covers/" + uniqueFileName;
                }

                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Book '{book.Title}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating book.");
                TempData["ErrorMessage"] = "An unexpected error occurred while creating the book.";
                return View(model);
            }
        }

        // GET: Books/Edit/{id}
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            var model = new BookViewModel
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                Genre = book.Genre,
                Description = book.Description,
                PublicationDate = book.PublicationDate,
                Price = book.Price,
                Quantity = book.Quantity,
                CoverImageUrl = book.CoverImageUrl,
                ISBN = book.ISBN,
                Language = book.Language,
                Format = book.Format,
                Publisher = book.Publisher,
                IsPhysicalLibraryAccess = book.IsPhysicalLibraryAccess,
                IsBestseller = book.IsBestseller,
                IsAwardWinner = book.IsAwardWinner,
                IsComingSoon = book.IsComingSoon,
                ReleaseDate = book.ReleaseDate
            };

            return View(model);
        }

        // POST: Books/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BookViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            // Allow model to be valid even if no new image is uploaded and an old one exists
            if (model.CoverImage == null && !string.IsNullOrEmpty(model.CoverImageUrl))
            {
                ModelState.Remove(nameof(model.CoverImage));
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors in the form.";
                return View(model);
            }

            try
            {
                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    return NotFound();
                }

                // Update core fields with defaults for empty values
                book.Title = string.IsNullOrWhiteSpace(model.Title) ? "Untitled" : model.Title?.Trim();
                book.Author = string.IsNullOrWhiteSpace(model.Author) ? "Unknown Author" : model.Author?.Trim();
                book.Genre = model.Genre?.Trim();
                book.Description = model.Description?.Trim();
                book.ISBN = model.ISBN?.Trim();
                book.Language = model.Language?.Trim();
                book.Format = model.Format?.Trim();
                book.Publisher = model.Publisher?.Trim();
                book.Price = model.Price;
                book.Quantity = model.Quantity;
                book.IsPhysicalLibraryAccess = model.IsPhysicalLibraryAccess;
                book.IsBestseller = model.IsBestseller;
                book.IsAwardWinner = model.IsAwardWinner;
                book.IsComingSoon = model.IsComingSoon;
                book.AddedDate = book.AddedDate; // Preserve original AddedDate
                book.PublicationDate = (model.PublicationDate ?? DateTime.UtcNow).ToUniversalTime();
                book.ReleaseDate = model.IsComingSoon && model.ReleaseDate.HasValue ? model.ReleaseDate.Value.ToUniversalTime() : null;

                // Handle cover image upload
                if (model.CoverImage != null && model.CoverImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images/book-covers");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.CoverImage.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.CoverImage.CopyToAsync(fileStream);
                    }

                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(book.CoverImageUrl))
                    {
                        var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, book.CoverImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    book.CoverImageUrl = "/images/book-covers/" + uniqueFileName;
                }

                _context.Update(book);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Book '{book.Title}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookExists(model.Id))
                {
                    return NotFound();
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating book with ID {BookId}.", id);
                TempData["ErrorMessage"] = "An error occurred while updating the book.";
                return View(model);
            }
        }

        // GET: Books/Delete/{id}
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // POST: Books/DeleteConfirmed/{id}
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    HandleError("Book not found");
                    return RedirectToAction(nameof(Index));
                }

                if (!string.IsNullOrEmpty(book.CoverImageUrl))
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, book.CoverImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _context.Books.Remove(book);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Book '{book.Title}' deleted successfully.";
            }
            catch (Exception ex)
            {
                HandleError("An error occurred while deleting the book", ex, id.ToString());
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Books/ManageDiscounts/{id}
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ManageDiscounts(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            var discount = await _context.TimedDiscounts
                .FirstOrDefaultAsync(td => td.BookId == id);

            var model = new TimedDiscountViewModel
            {
                BookId = book.Id,
                BookTitle = book.Title,
                DiscountPercentage = discount?.DiscountPercentage * 100 ?? 0,
                StartDate = discount?.StartDate ?? DateTime.UtcNow,
                ExpiresAt = discount?.ExpiresAt ?? DateTime.UtcNow.AddDays(7),
                OnSaleFlag = discount?.OnSaleFlag ?? false
            };

            return View(model);
        }

        // POST: Books/ManageDiscounts
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageDiscounts(TimedDiscountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var book = await _context.Books.FindAsync(model.BookId);
            if (book == null)
            {
                return NotFound();
            }

            var existingDiscount = await _context.TimedDiscounts
                .FirstOrDefaultAsync(td => td.BookId == model.BookId);

            if (model.DiscountPercentage > 0)
            {
                if (existingDiscount == null)
                {
                    var discount = new TimedDiscount
                    {
                        BookId = model.BookId,
                        DiscountPercentage = model.DiscountPercentage / 100,
                        StartDate = model.StartDate,
                        ExpiresAt = model.ExpiresAt,
                        OnSaleFlag = model.OnSaleFlag
                    };
                    _context.TimedDiscounts.Add(discount);
                }
                else
                {
                    existingDiscount.DiscountPercentage = model.DiscountPercentage / 100;
                    existingDiscount.StartDate = model.StartDate;
                    existingDiscount.ExpiresAt = model.ExpiresAt;
                    existingDiscount.OnSaleFlag = model.OnSaleFlag;
                    _context.TimedDiscounts.Update(existingDiscount);
                }
            }
            else if (existingDiscount != null)
            {
                _context.TimedDiscounts.Remove(existingDiscount);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Discount updated successfully.";
            return RedirectToAction("Details", new { id = model.BookId });
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }
    }
}