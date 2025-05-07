using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using BookHive.Hubs;
using BookNook.Data;
using BookNook.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BookNook.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminController> _logger;
        private readonly IHubContext<AnnouncementHub> _announcementHub;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AdminController> logger,
            IHubContext<AnnouncementHub> announcementHub)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _announcementHub = announcementHub;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            foreach (var u in users) u.Roles = (await _userManager.GetRolesAsync(u)).ToList();
            return View(users);
        }

        public async Task<IActionResult> ViewProfile(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("User ID is required.");
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound("User not found.");
            ViewBag.Roles = await _userManager.GetRolesAsync(user);
            return View(user);
        }

        public IActionResult SendDeletionEmail(string id) => RedirectToAction("SendDeletionNotice", new { id });

        public async Task<IActionResult> SendDeletionNotice(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("User ID is required.");
            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null || currentAdmin.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound("User not found.");
            return View(new DeletionNoticeViewModel { UserId = user.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDeletionNotice(DeletionNoticeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound("User not found.");

            try
            {
                if (string.IsNullOrEmpty(user.Email))
                {
                    TempData["ErrorMessage"] = "User email is not set.";
                    return RedirectToAction(nameof(Index));
                }

                await SendEmailAsync(
                    user.Email,
                    "Deletion Notice from BookNook",
                    $"<p>Dear {user.FirstName ?? "User"},</p>" +
                    $"<p>You have received a deletion notice from the BookNook Admin:</p>" +
                    $"<p>{model.Message}</p>" +
                    $"<p>Please respond to this email or take appropriate action to avoid account deletion.</p>" +
                    $"<p>Best regards,<br>BookNook Admin</p>");

                TempData["PendingDeletionUserId"] = user.Id;
                TempData["SuccessMessage"] = $"Deletion notice sent to {user.Email}. Waiting for user response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send deletion notice to {Email}", user.Email);
                TempData["ErrorMessage"] = $"Failed to send deletion notice: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> CancelDeletion(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("User ID is required.");
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound("User not found.");

            try
            {
                if (string.IsNullOrEmpty(user.Email))
                {
                    TempData["ErrorMessage"] = "User email is not set.";
                    return RedirectToAction(nameof(Index));
                }

                await SendEmailAsync(
                    user.Email,
                    "Deletion Notice Canceled - BookNook",
                    $"<p>Dear {user.FirstName ?? "User"},</p>" +
                    $"<p>We are pleased to inform you that the deletion notice for your account has been canceled.</p>" +
                    $"<p>Thank you for addressing the concerns. Your account remains active.</p>" +
                    $"<p>Best regards,<br>BookNook Admin</p>");

                TempData["SuccessMessage"] = $"Deletion notice for {user.Email} has been canceled.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cancellation notice to {Email}", user.Email);
                TempData["ErrorMessage"] = $"Failed to send cancellation notice: {ex.Message}";
            }

            TempData["PendingDeletionUserId"] = null;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ConfirmDeletion(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("User ID is required.");
            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null || currentAdmin.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("User ID is required.");
            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null || currentAdmin.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrEmpty(user.ProfileImageUrl))
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfileImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            var result = await _userManager.DeleteAsync(user);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? $"User {user.Email ?? user.UserName} has been deleted."
                    : $"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}";

            TempData["PendingDeletionUserId"] = null;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "Email and password are required.";
                return RedirectToAction(nameof(Index));
            }

            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Staff"))
                    await _roleManager.CreateAsync(new IdentityRole("Staff"));

                await _userManager.AddToRoleAsync(user, "Staff");

                try
                {
                    await SendEmailAsync(
                        email,
                        "Staff Account Created - BookNook",
                        $"<p>Dear Staff,</p>" +
                        $"<p>Your staff account has been created. Please log in with the following credentials:</p>" +
                        $"<p>Email: {email}</p>" +
                        $"<p>Password: {password}</p>" +
                        $"<p>Best regards,<br>BookNook Admin</p>");
                    TempData["SuccessMessage"] = $"Staff account created for {email}.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send staff account creation email to {Email}", email);
                    TempData["SuccessMessage"] = $"Staff account created for {email}, but failed to send email: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SendAnnouncement(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                TempData["ErrorMessage"] = "Announcement message cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _announcementHub.Clients.All.SendAsync("ReceiveAnnouncement", message);
                TempData["SuccessMessage"] = "Announcement sent successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send announcement");
                TempData["ErrorMessage"] = $"Failed to send announcement: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult CreateTimedAnnouncement() => View(new TimedAnnouncement());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTimedAnnouncement(TimedAnnouncement model)
        {
            if (!ModelState.IsValid) return View(model);
            if (string.IsNullOrEmpty(model.Message))
            {
                ModelState.AddModelError("Message", "Message is required.");
                return View(model);
            }

            model.ExpiresAt = model.ExpiresAt.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(model.ExpiresAt, DateTimeKind.Local).ToUniversalTime(),
                DateTimeKind.Local => model.ExpiresAt.ToUniversalTime(),
                _ => model.ExpiresAt
            };

            if (model.ExpiresAt <= DateTime.UtcNow)
            {
                ModelState.AddModelError("ExpiresAt", "Expiration time must be in the future.");
                return View(model);
            }

            model.CreatedAt = DateTime.UtcNow;
            _context.TimedAnnouncements.Add(model);
            await _context.SaveChangesAsync();

            await _announcementHub.Clients.All.SendAsync("ReceiveTimedAnnouncement",
                                                         model.Message, model.CreatedAt, model.ExpiresAt);

            TempData["SuccessMessage"] = "Timed announcement created successfully!";
            return RedirectToAction(nameof(Index));
        }

        private async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Attempted to send email with null or empty address.");
                return;
            }

            try
            {
                var host = _configuration["Smtp:Host"];
                var port = _configuration["Smtp:Port"];
                var user = _configuration["Smtp:Username"];
                var pass = _configuration["Smtp:Password"];

                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port) ||
                    string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                    throw new InvalidOperationException("SMTP configuration is missing or incomplete in appsettings.json.");

                var client = new SmtpClient(host)
                {
                    Port = int.Parse(port),
                    Credentials = new NetworkCredential(user, pass),
                    EnableSsl = true
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(user),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };
                mail.To.Add(email);

                await client.SendMailAsync(mail);
                _logger.LogInformation("Email sent successfully to {Email} with subject: {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} with subject: {Subject}", email, subject);
                throw;
            }
        }
    }
}
