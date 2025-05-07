using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BookNook.Data;      // ← updated
using BookNook.Models;    // ← updated

namespace BookNook.Data    // ← updated
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            /* ----------  roles ---------- */
            string[] roles = { "Admin", "Staff", "User" };
            foreach (var role in roles)
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));

            /* ----------  default admin ---------- */
            var adminEmail = "admin@booknook.com";
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User"
                };
                var res = await userManager.CreateAsync(admin, "Admin@123");
                if (res.Succeeded) await userManager.AddToRoleAsync(admin, "Admin");
                else throw new Exception($"Cannot create admin: {string.Join(", ", res.Errors.Select(e => e.Description))}");
            }

            /* ----------  second admin ---------- */
            var secondEmail = "newadmin@booknook.com";
            var second = await userManager.FindByEmailAsync(secondEmail);
            if (second == null)
            {
                second = new ApplicationUser
                {
                    UserName = secondEmail,
                    Email = secondEmail,
                    EmailConfirmed = true
                };
                var res = await userManager.CreateAsync(second, "NewAdminPassword123!");
                if (res.Succeeded) await userManager.AddToRoleAsync(second, "Admin");
                else throw new Exception($"Cannot create 2nd admin: {string.Join(", ", res.Errors.Select(e => e.Description))}");
            }

            /* ----------  seed books (if none) ---------- */
            if (!context.Books.Any())
            {
                context.Books.AddRange(
                    new Book
                    {
                        Title = "To Kill a Mockingbird",
                        Author = "Harper Lee",
                        Description = "A gripping tale of racial injustice and the loss of innocence in a small Southern town.",
                        CoverImageUrl = "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?q=80&w=1974&auto=format&fit=crop",
                        AddedDate = DateTime.UtcNow.AddDays(-10),
                        Price = 10.99m,
                        Quantity = 100,
                        Format = "Paperback",
                        Genre = "Fiction",
                        PublicationDate = new DateTime(1960, 7, 11, 0, 0, 0, DateTimeKind.Utc),
                        ISBN = "978-0446310789",
                        Language = "English",
                        Publisher = "J.B. Lippincott & Co.",
                        IsPhysicalLibraryAccess = true,
                        IsBestseller = true,
                        IsAwardWinner = true
                    },
                    new Book
                    {
                        Title = "1984",
                        Author = "George Orwell",
                        Description = "A dystopian novel about totalitarian surveillance and control.",
                        CoverImageUrl = "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?q=80&w=1974&auto=format&fit=crop",
                        AddedDate = DateTime.UtcNow.AddDays(-5),
                        Price = 12.99m,
                        Quantity = 80,
                        Format = "Hardcover",
                        Genre = "Science Fiction",
                        PublicationDate = new DateTime(1949, 6, 8, 0, 0, 0, DateTimeKind.Utc),
                        ISBN = "978-0451524935",
                        Language = "English",
                        Publisher = "Secker & Warburg",
                        IsPhysicalLibraryAccess = false,
                        IsBestseller = true
                    },
                    new Book
                    {
                        Title = "Pride and Prejudice",
                        Author = "Jane Austen",
                        Description = "A romantic novel about the Bennet sisters and their marriage prospects.",
                        CoverImageUrl = "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?q=80&w=1974&auto=format&fit=crop",
                        AddedDate = DateTime.UtcNow.AddDays(-2),
                        Price = 9.99m,
                        Quantity = 120,
                        Format = "Ebook",
                        Genre = "Romance",
                        PublicationDate = new DateTime(1813, 1, 28, 0, 0, 0, DateTimeKind.Utc),
                        ISBN = "978-0141439518",
                        Language = "English",
                        Publisher = "T. Egerton",
                        IsPhysicalLibraryAccess = true
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
