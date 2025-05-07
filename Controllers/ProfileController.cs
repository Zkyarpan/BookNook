using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using BookNook.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BookNook.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<ProfileController> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");
            return View(user);
        }

        public async Task<IActionResult> ViewProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var model = new ProfileViewModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfileImageUrl = user.ProfileImageUrl
            };
            return View(model);
        }

        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var model = new ProfileViewModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfileImageUrl = user.ProfileImageUrl
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            try
            {
                user.UserName = model.UserName ?? string.Empty;
                user.Email = model.Email ?? string.Empty;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;

                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    if (model.ProfileImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ProfileImage", "Image size must not exceed 5MB.");
                        return View(model);
                    }

                    var folder = Path.Combine(_env.WebRootPath, "images/profile-pics");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    var name = $"{Guid.NewGuid()}_{Path.GetFileName(model.ProfileImage.FileName)}";
                    var path = Path.Combine(folder, name);

                    await using (var fs = new FileStream(path, FileMode.Create))
                    {
                        await model.ProfileImage.CopyToAsync(fs);
                    }

                    if (!string.IsNullOrEmpty(user.ProfileImageUrl))
                    {
                        var old = Path.Combine(_env.WebRootPath, user.ProfileImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
                    }

                    user.ProfileImageUrl = "/images/profile-pics/" + name;
                }

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    TempData["SuccessMessage"] = "Profile updated successfully.";
                    return RedirectToAction("ViewProfile");
                }

                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profile update error for user {Id}", user.Id);
                TempData["ErrorMessage"] = "An error occurred while updating your profile.";
            }

            return View(model);
        }

        public IActionResult ChangePassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");
            if (string.IsNullOrEmpty(user.Email)) { TempData["ErrorMessage"] = "User email is not set."; return RedirectToAction("ViewProfile"); }

            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var link = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);
                if (link == null) { TempData["ErrorMessage"] = "Failed to generate password reset link."; return RedirectToAction("ViewProfile"); }

                await SendEmailAsync(
                    user.Email,
                    "Reset Your BookNook Password",
                    $"<p>Dear {user.FirstName ?? "User"},</p><p>Please reset your password by <a href='{HtmlEncoder.Default.Encode(link)}'>clicking here</a>.</p><p>This link will expire in 24 hours.</p><p>Best regards,<br>BookNook Team</p>");

                TempData["SuccessMessage"] = "Password reset link sent to your email.";
                return RedirectToAction("ViewProfile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset email error for user {Id}", user.Id);
                TempData["ErrorMessage"] = "Failed to send password reset email.";
                return RedirectToAction("ViewProfile");
            }
        }

        private async Task SendEmailAsync(string email, string subject, string html)
        {
            var host = _config["Smtp:Host"];
            var port = _config["Smtp:Port"];
            var user = _config["Smtp:Username"];
            var pass = _config["Smtp:Password"];
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                throw new InvalidOperationException("SMTP configuration missing.");

            var client = new SmtpClient(host) { Port = int.Parse(port), Credentials = new NetworkCredential(user, pass), EnableSsl = true };
            var msg = new MailMessage { From = new MailAddress(user), Subject = subject, Body = html, IsBodyHtml = true };
            msg.To.Add(email);
            await client.SendMailAsync(msg);
        }
    }
}
