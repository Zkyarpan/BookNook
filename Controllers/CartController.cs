using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BookNook.Data;
using BookNook.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using BookHive.Hubs;

namespace BookNook.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CartController> _logger;
        private readonly IConfiguration _config;
        private readonly IHubContext<AnnouncementHub> _announceHub;
        private readonly IHubContext<OrderNotificationHub> _orderHub;

        public CartController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<CartController> logger,
            IConfiguration config,
            IHubContext<AnnouncementHub> announceHub,
            IHubContext<OrderNotificationHub> orderHub)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _announceHub = announceHub ?? throw new ArgumentNullException(nameof(announceHub));
            _orderHub = orderHub ?? throw new ArgumentNullException(nameof(orderHub));
        }

        public async Task<IActionResult> Cart()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var items = await _context.Carts.Where(c => c.UserId == user.Id).Include(c => c.Book).ToListAsync();
            foreach (var i in items) await _context.Entry(i.Book).ReloadAsync();

            var ids = items.Select(i => i.BookId).ToList();
            var discounts = await _context.TimedDiscounts.Where(td => ids.Contains(td.BookId)).ToListAsync();

            var list = items.Select(i =>
            {
                var d = discounts.FirstOrDefault(td => td.BookId == i.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                return new CartWithDiscountViewModel
                {
                    CartItem = i,
                    OnSaleFlag = d?.OnSaleFlag ?? false,
                    IsDiscountActive = d != null,
                    DiscountedPrice = d != null ? i.Book.Price * (1 - d.DiscountPercentage) : i.Book.Price
                };
            }).ToList();

            return View("~/Views/User/Cart.cshtml", list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCartQuantity(int bookId, int quantity)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found." });

            var item = await _context.Carts.Include(c => c.Book).FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == bookId);
            if (item == null) return Json(new { success = false, message = "Book not in cart." });

            await _context.Entry(item.Book).ReloadAsync();
            if (quantity < 1) return Json(new { success = false, message = "Quantity must be at least 1." });
            if (quantity > item.Book.Quantity) return Json(new { success = false, message = $"Only {item.Book.Quantity} copies available." });

            item.Quantity = quantity;
            await _context.SaveChangesAsync();

            var count = await _context.Carts.Where(c => c.UserId == user.Id).Select(c => c.BookId).Distinct().CountAsync();
            await _orderHub.Clients.All.SendAsync("UpdateCartCount", count);

            return Json(new { success = true, message = "Cart updated." });
        }

        [HttpGet]
        public async Task<IActionResult> GetCartItem(int bookId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { exists = false });
            var item = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == bookId);
            return Json(new { exists = item != null, quantity = item?.Quantity ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int bookId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var entry = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == bookId);
            if (entry == null)
            {
                TempData["ErrorMessage"] = "Book not found in cart.";
                return RedirectToAction(nameof(Cart));
            }

            _context.Carts.Remove(entry);
            await _context.SaveChangesAsync();

            var count = await _context.Carts.Where(c => c.UserId == user.Id).Select(c => c.BookId).Distinct().CountAsync();
            await _orderHub.Clients.All.SendAsync("UpdateCartCount", count);

            TempData["SuccessMessage"] = "Book removed from cart.";
            return RedirectToAction(nameof(Cart));
        }

        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var items = await _context.Carts.Where(c => c.UserId == user.Id).Include(c => c.Book).ToListAsync();
            if (!items.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Cart));
            }

            foreach (var i in items)
                if (i.Book.Quantity < i.Quantity)
                {
                    TempData["ErrorMessage"] = $"Not enough stock for '{i.Book.Title}'.";
                    return RedirectToAction(nameof(Cart));
                }

            var ids = items.Select(i => i.BookId).ToList();
            var discounts = await _context.TimedDiscounts.Where(td => ids.Contains(td.BookId)).ToListAsync();

            decimal subtotal = 0;
            var list = new List<(Cart cart, decimal price, bool sale)>();

            foreach (var i in items)
            {
                var d = discounts.FirstOrDefault(td => td.BookId == i.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                var price = d != null ? i.Book.Price * (1 - d.DiscountPercentage) : i.Book.Price;
                subtotal += price * i.Quantity;
                list.Add((i, price, d?.OnSaleFlag ?? false));
            }

            int orderCount = await _context.Orders.CountAsync(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received");
            int totalItems = items.Sum(i => i.Quantity);
            decimal qDisc = totalItems >= 5 ? 0.05m : 0m;
            decimal lDisc = orderCount >= 10 ? 0.10m : 0m;
            decimal combined = qDisc + lDisc - qDisc * lDisc;
            decimal discountAmount = subtotal * combined;
            decimal final = subtotal - discountAmount;

            ViewBag.TotalPrice = subtotal;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.FinalPrice = final;
            ViewBag.QuantityDiscount = qDisc * 100;
            ViewBag.LoyaltyDiscount = lDisc * 100;
            ViewBag.OrderCount = orderCount;
            ViewBag.TotalItems = totalItems;

            return View("~/Views/User/Checkout.cshtml", list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CheckoutConfirmed()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var items = await _context.Carts.Where(c => c.UserId == user.Id).Include(c => c.Book).ToListAsync();
            if (!items.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Cart));
            }

            foreach (var i in items)
                if (i.Book.Quantity < i.Quantity)
                {
                    TempData["ErrorMessage"] = $"Not enough stock for '{i.Book.Title}'.";
                    return RedirectToAction(nameof(Cart));
                }

            var ids = items.Select(i => i.BookId).ToList();
            var discounts = await _context.TimedDiscounts.Where(td => ids.Contains(td.BookId)).ToListAsync();

            decimal subtotal = 0;

            foreach (var i in items)
            {
                var d = discounts.FirstOrDefault(td => td.BookId == i.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                subtotal += (d != null ? i.Book.Price * (1 - d.DiscountPercentage) : i.Book.Price) * i.Quantity;
            }

            int orderCount = await _context.Orders.CountAsync(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received");
            int totalItems = items.Sum(i => i.Quantity);
            decimal qDisc = totalItems >= 5 ? 0.05m : 0m;
            decimal lDisc = orderCount >= 10 ? 0.10m : 0m;
            decimal combined = qDisc + lDisc - qDisc * lDisc;
            decimal discountAmount = subtotal * combined;
            decimal final = subtotal - discountAmount;

            var body = new StringBuilder();
            body.AppendLine("<h2>Order Confirmation</h2>");
            body.AppendLine("<table border='1' cellpadding='5'><tr><th>Book</th><th>Qty</th><th>Price</th><th>Claim Code</th></tr>");

            int globalCount = await _context.Orders.CountAsync();

            foreach (var i in items)
            {
                var d = discounts.FirstOrDefault(td => td.BookId == i.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                var price = d != null ? i.Book.Price * (1 - d.DiscountPercentage) : i.Book.Price;
                var order = new Order
                {
                    UserId = user.Id,
                    BookId = i.BookId,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = price * i.Quantity * (1 - combined),
                    Quantity = i.Quantity,
                    IsCancelled = false,
                    ClaimCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                    Status = "Placed"
                };
                _context.Orders.Add(order);
                i.Book.Quantity -= i.Quantity;

                body.AppendLine($"<tr><td>{i.Book.Title}</td><td>{i.Quantity}</td><td>${order.TotalPrice:F2}</td><td>{order.ClaimCode}</td></tr>");

                await _announceHub.Clients.All.SendAsync("ReceiveAnnouncement", $"Order for '{i.Book.Title}' placed! Order #{++globalCount}");
            }

            body.AppendLine("</table>");
            body.AppendLine($"<p>Total: ${final:F2}</p>");

            try { await SendEmailAsync(user.Email, "BookNook Order Confirmation", body.ToString()); } catch (Exception ex) { _logger.LogError(ex, "Email failed"); }

            _context.Carts.RemoveRange(items);
            await _context.SaveChangesAsync();

            int updatedCount = await _context.Orders.Where(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received").CountAsync();
            await _orderHub.Clients.All.SendAsync("UpdateOrderCount", updatedCount);

            TempData["SuccessMessage"] = "Order placed successfully.";
            return RedirectToAction("MyOrders", "Orders");
        }

        public async Task<IActionResult> Whitelist()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var books = await _context.Whitelists.Where(w => w.UserId == user.Id).Include(w => w.Book).Select(w => w.Book).ToListAsync();
            var ids = books.Select(b => b.Id).ToList();
            var discounts = await _context.TimedDiscounts.Where(td => ids.Contains(td.BookId)).ToListAsync();

            var list = books.Select(b =>
            {
                var d = discounts.FirstOrDefault(td => td.BookId == b.Id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                return new BookWithDiscountViewModel
                {
                    Book = b,
                    OnSaleFlag = d?.OnSaleFlag ?? false,
                    IsDiscountActive = d != null,
                    DiscountedPrice = d != null ? b.Price * (1 - d.DiscountPercentage) : b.Price
                };
            }).ToList();

            return View("~/Views/User/Whitelist.cshtml", list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromWhitelist(int bookId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var entry = await _context.Whitelists.FirstOrDefaultAsync(w => w.UserId == user.Id && w.BookId == bookId);
            if (entry == null)
            {
                TempData["ErrorMessage"] = "Book not in whitelist.";
                return RedirectToAction(nameof(Whitelist));
            }

            _context.Whitelists.Remove(entry);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Book removed from whitelist.";
            return RedirectToAction(nameof(Whitelist));
        }

        private async Task SendEmailAsync(string to, string subject, string body)
        {
            var host = _config["Smtp:Host"];
            var port = int.Parse(_config["Smtp:Port"]);
            var user = _config["Smtp:Username"];
            var pass = _config["Smtp:Password"];

            var client = new SmtpClient(host) { Port = port, Credentials = new NetworkCredential(user, pass), EnableSsl = true };
            var msg = new MailMessage { From = new MailAddress(user), Subject = subject, Body = body, IsBodyHtml = true };
            msg.To.Add(to);
            await client.SendMailAsync(msg);
        }
    }
}
