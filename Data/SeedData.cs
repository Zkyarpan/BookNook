using BookNook.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookNook.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Seed Roles
            string[] roleNames = { "Admin", "Staff", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Seed Admin User (admin@bookhive.com)
            var adminEmail = "admin@bookhive.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User"
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            // Seed New Admin User (newadmin@example.com)
            var newAdminEmail = "newadmin@example.com";
            var newAdminUser = await userManager.FindByEmailAsync(newAdminEmail);
            if (newAdminUser == null)
            {
                newAdminUser = new ApplicationUser
                {
                    UserName = newAdminEmail,
                    Email = newAdminEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(newAdminUser, "NewAdminPassword123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdminUser, "Admin");
                }
                else
                {
                    throw new Exception($"Failed to create new admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            // Seed Books
            if (!context.Books.Any())
            {
                context.Books.AddRange(
                    new Book
                    {
                        Title = "To Kill a Mockingbird",
                        Author = "Harper Lee",
                        Description = "A gripping tale of racial injustice and the loss of innocence in a small Southern town.",
                        CoverImageUrl = "https://images.unsplash.com/photo-1544947950-fa07a98d237f?q=80&w=1974&auto=format&fit=crop",
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
                        CoverImageUrl = "https://images.unsplash.com/photo-1541963463532-d68292c34b19?q=80&w=1976&auto=format&fit=crop",
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
                        IsBestseller = true,
                        IsAwardWinner = false
                    },
                    new Book
                    {
                        Title = "Pride and Prejudice",
                        Author = "Jane Austen",
                        Description = "A romantic novel about the Bennet sisters and their marriage prospects.",
                        CoverImageUrl = "https://images.unsplash.com/photo-1589829085413-56de8ae18c73?q=80&w=1992&auto=format&fit=crop",
                        AddedDate = DateTime.UtcNow.AddDays(-2),
                        Price = 9.99m,
                        Quantity = 120,
                        Format = "Ebook",
                        Genre = "Romance",
                        PublicationDate = new DateTime(1813, 1, 28, 0, 0, 0, DateTimeKind.Utc),
                        ISBN = "978-0141439518",
                        Language = "English",
                        Publisher = "T. Egerton",
                        IsPhysicalLibraryAccess = true,
                        IsBestseller = false,
                        IsAwardWinner = false
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}