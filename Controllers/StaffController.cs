using System;
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
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StaffController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<OrderNotificationHub> _hubContext;

        public StaffController(
            ApplicationDbContext context,
            ILogger<StaffController> logger,
            UserManager<ApplicationUser> userManager,
            IHubContext<OrderNotificationHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public IActionResult FulfillOrder() => View(new FulfillOrderViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FulfillOrder(FulfillOrderViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var order = await _context.Orders
                .Include(o => o.Book)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.ClaimCode == model.ClaimCode);

            if (order == null)
            {
                ModelState.AddModelError("ClaimCode", "Invalid claim code.");
                return View(model);
            }

            if (order.IsCancelled)
            {
                ModelState.AddModelError("ClaimCode", "This order has been cancelled and cannot be fulfilled.");
                return View(model);
            }

            if (order.IsFulfilled)
            {
                ModelState.AddModelError("ClaimCode", "This order has already been fulfilled.");
                return View(model);
            }

            if (order.UserId != model.UserId)
            {
                ModelState.AddModelError("UserId", "The user ID does not match the order.");
                return View(model);
            }

            model.Order = order;
            model.IsConfirmationStep = true;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmFulfillOrder(string claimCode, string userId)
        {
            var order = await _context.Orders
                .Include(o => o.Book)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.ClaimCode == claimCode);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Invalid claim code.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            if (order.UserId != userId)
            {
                TempData["ErrorMessage"] = "The user ID does not match the order.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            if (order.IsCancelled)
            {
                TempData["ErrorMessage"] = "This order has been cancelled and cannot be fulfilled.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            if (order.IsFulfilled)
            {
                TempData["ErrorMessage"] = "This order has already been fulfilled.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            order.IsFulfilled = true;
            order.FulfilledAt = DateTime.UtcNow;
            order.Status = "Received";

            await _context.SaveChangesAsync();

            var message = $"Order for '{order.Book.Title}' by {order.User.FirstName} {order.User.LastName} has been successfully fulfilled!";
            await _hubContext.Clients.All.SendAsync("ReceiveOrderNotification", message);

            TempData["SuccessMessage"] = "Order fulfilled successfully.";
            return RedirectToAction(nameof(FulfillOrder));
        }
    }

    public class FulfillOrderViewModel
    {
        public string ClaimCode { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public Order? Order { get; set; }
        public bool IsConfirmationStep { get; set; }
    }
}
