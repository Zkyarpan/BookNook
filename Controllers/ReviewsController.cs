using System;
using System.Threading.Tasks;
using BookNook.Data;
using BookNook.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookNook.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReviewsController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(
            ApplicationDbContext context,
            ILogger<ReviewsController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> Create(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var hasPurchased = await _context.Orders.AnyAsync(o => o.UserId == user.Id && o.BookId == bookId && !o.IsCancelled);
            if (!hasPurchased)
            {
                TempData["ErrorMessage"] = "You can only review books you have purchased.";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            var reviewed = await _context.Reviews.AnyAsync(r => r.UserId == user.Id && r.BookId == bookId && r.ParentReviewId == null);
            if (reviewed)
            {
                TempData["ErrorMessage"] = "You have already reviewed this book.";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            return View(new Review { BookId = bookId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> Create([Bind("BookId,Rating,Comment")] Review review)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found." });

            if (review.Rating < 1 || review.Rating > 5)
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = false, message = "Rating must be between 1 and 5." })
                    : BadRequest();

            if (string.IsNullOrWhiteSpace(review.Comment))
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = false, message = "Comment is required." })
                    : BadRequest();

            var book = await _context.Books.FindAsync(review.BookId);
            if (book == null)
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = false, message = "Book not found." })
                    : NotFound();

            var purchased = await _context.Orders.AnyAsync(o => o.UserId == user.Id && o.BookId == review.BookId && !o.IsCancelled);
            if (!purchased)
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = false, message = "You can only review books you have purchased." })
                    : Unauthorized();

            var exists = await _context.Reviews.AnyAsync(r => r.UserId == user.Id && r.BookId == review.BookId && r.ParentReviewId == null);
            if (exists)
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = false, message = "You have already reviewed this book." })
                    : Conflict();

            review.UserId = user.Id;
            review.ReviewDate = DateTime.UtcNow;
            review.ParentReviewId = null;

            try
            {
                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving review");
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = false, message = "Error saving review." })
                    : StatusCode(500);
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new
                {
                    success = true,
                    message = "Review submitted successfully.",
                    review = new
                    {
                        userName = user.UserName,
                        rating = review.Rating,
                        comment = review.Comment,
                        reviewDate = review.ReviewDate.ToString("d MMM yyyy")
                    }
                });

            TempData["SuccessMessage"] = "Review submitted successfully.";
            return RedirectToAction("Details", "Books", new { id = review.BookId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CreateReply(int bookId, int parentReviewId, string comment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found." });

            var book = await _context.Books.FindAsync(bookId);
            if (book == null) return Json(new { success = false, message = "Book not found." });

            var parent = await _context.Reviews.FindAsync(parentReviewId);
            if (parent == null || parent.BookId != bookId || parent.ParentReviewId != null)
                return Json(new { success = false, message = "Parent review not found." });

            var purchased = await _context.Orders.AnyAsync(o => o.UserId == user.Id && o.BookId == bookId && !o.IsCancelled);
            if (!purchased) return Json(new { success = false, message = "Purchase required to reply." });

            if (string.IsNullOrWhiteSpace(comment)) return Json(new { success = false, message = "Comment is required." });

            var reply = new Review
            {
                BookId = bookId,
                UserId = user.Id,
                Rating = 0,
                Comment = comment,
                ReviewDate = DateTime.UtcNow,
                ParentReviewId = parentReviewId
            };

            try
            {
                _context.Reviews.Add(reply);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving reply");
                return Json(new { success = false, message = "Error saving reply." });
            }

            return Json(new
            {
                success = true,
                message = "Reply submitted successfully.",
                reply = new
                {
                    comment = reply.Comment,
                    userName = user.UserName,
                    reviewDate = reply.ReviewDate.ToString("d MMM yyyy")
                }
            });
        }
    }
}
