using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BookHive.Hubs;
using BookNook.Data;
using BookNook.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookNook.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly IHubContext<AnnouncementHub> _hubContext;
        private readonly IHubContext<OrderNotificationHub> _orderHubContext;

        public OrdersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<OrdersController> logger,
            IHubContext<AnnouncementHub> hubContext,
            IHubContext<OrderNotificationHub> orderHubContext)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _orderHubContext = orderHubContext ?? throw new ArgumentNullException(nameof(orderHubContext));
        }

        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var orders = await _context.Orders
                .Where(o => o.UserId == user.Id)
                .Include(o => o.Book)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View("~/Views/User/MyOrders.cshtml", orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CancelOrder(string userId, int bookId, DateTime orderDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Id != userId) return NotFound("User not found or unauthorized.");

            var order = await _context.Orders
                .Include(o => o.Book)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.BookId == bookId && o.OrderDate == orderDate);

            if (order == null) { TempData["ErrorMessage"] = "Order not found."; return RedirectToAction(nameof(MyOrders)); }

            if (order.Status == "Received" || order.IsCancelled) return await DeleteOrder(userId, bookId, orderDate);

            if (!order.IsCancellable) { TempData["ErrorMessage"] = "This order cannot be cancelled."; return RedirectToAction(nameof(MyOrders)); }

            order.IsCancelled = true;
            order.CancelledAt = DateTime.UtcNow;
            order.Status = "Cancelled";
            order.Book.Quantity += order.Quantity;

            await _context.SaveChangesAsync();
            await UpdateOrderCount(user.Id);

            TempData["SuccessMessage"] = "Order cancelled successfully.";
            return RedirectToAction(nameof(MyOrders));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> DeleteOrder(string userId, int bookId, DateTime orderDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Id != userId) return NotFound("User not found or unauthorized.");

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.UserId == userId && o.BookId == bookId && o.OrderDate == orderDate);

            if (order == null) { TempData["ErrorMessage"] = "Order not found."; return RedirectToAction(nameof(MyOrders)); }

            if (order.Status != "Received" && !order.IsCancelled)
            {
                TempData["ErrorMessage"] = "Only received or cancelled orders can be deleted.";
                return RedirectToAction(nameof(MyOrders));
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            await UpdateOrderCount(user.Id);

            TempData["SuccessMessage"] = "Order deleted successfully.";
            return RedirectToAction(nameof(MyOrders));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CancelOrders(string[] selectedOrders)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");
            if (selectedOrders == null || !selectedOrders.Any()) { TempData["ErrorMessage"] = "No orders selected."; return RedirectToAction(nameof(MyOrders)); }

            int cancelled = 0;
            foreach (var sel in selectedOrders)
            {
                var parts = sel.Split('|');
                if (parts.Length != 3 || parts[0] != user.Id) continue;
                if (!int.TryParse(parts[1], out var bookId)) continue;
                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) continue;
                dt = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

                var order = await _context.Orders.Include(o => o.Book)
                    .FirstOrDefaultAsync(o => o.UserId == user.Id && o.BookId == bookId && o.OrderDate == dt);
                if (order == null || order.IsCancelled || order.Status == "Received" || !order.IsCancellable) continue;

                order.IsCancelled = true;
                order.CancelledAt = DateTime.UtcNow;
                order.Status = "Cancelled";
                order.Book.Quantity += order.Quantity;
                cancelled++;
            }

            await _context.SaveChangesAsync();
            await DeleteOrders(selectedOrders);
            await UpdateOrderCount(user.Id);

            TempData["SuccessMessage"] = cancelled > 0 ? $"{cancelled} order(s) cancelled." : "No orders were cancelled.";
            return RedirectToAction(nameof(MyOrders));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> DeleteOrders(string[] selectedOrders)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) { TempData["ErrorMessage"] = "User not found."; return RedirectToAction(nameof(MyOrders)); }
            if (selectedOrders == null || !selectedOrders.Any()) { TempData["ErrorMessage"] = "No orders selected."; return RedirectToAction(nameof(MyOrders)); }

            int deleted = 0;
            foreach (var sel in selectedOrders)
            {
                var parts = sel.Split('|');
                if (parts.Length != 3 || parts[0] != user.Id) continue;
                if (!int.TryParse(parts[1], out var bookId)) continue;
                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) continue;
                dt = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

                var order = await _context.Orders.FirstOrDefaultAsync(o => o.UserId == user.Id && o.BookId == bookId && o.OrderDate == dt);
                if (order == null) continue;
                if (order.Status != "Received" && !order.IsCancelled) continue;

                _context.Orders.Remove(order);
                deleted++;
            }

            await _context.SaveChangesAsync();
            await UpdateOrderCount(user.Id);

            if (deleted > 0) TempData["SuccessMessage"] = (TempData["SuccessMessage"]?.ToString() + $" {deleted} order(s) deleted.").Trim();
            return RedirectToAction(nameof(MyOrders));
        }

        private async Task UpdateOrderCount(string userId)
        {
            var count = await _context.Orders.Where(o => o.UserId == userId && !o.IsCancelled && o.Status != "Received").CountAsync();
            await _orderHubContext.Clients.All.SendAsync("UpdateOrderCount", count);
        }
    }
}
