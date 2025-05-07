using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BookNook.Models;                

namespace BookNook.Data                
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Book> Books { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Whitelist> Whitelists { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<TimedDiscount> TimedDiscounts { get; set; }
        public DbSet<BookLoan> BookLoans { get; set; }
        public DbSet<TimedAnnouncement> TimedAnnouncements { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // composite keys
            builder.Entity<Cart>()
                   .HasKey(c => new { c.UserId, c.BookId });

            builder.Entity<Whitelist>()
                   .HasKey(w => new { w.UserId, w.BookId });

            builder.Entity<Order>()
                   .HasKey(o => new { o.UserId, o.BookId, o.OrderDate });

            builder.Entity<BookLoan>()
                   .HasKey(bl => new { bl.UserId, bl.BookId, bl.LoanDate });

            // reviews (self‑reference & relationships)
            builder.Entity<Review>()
                   .HasKey(r => r.Id);

            builder.Entity<Review>()
                   .HasOne(r => r.ParentReview)
                   .WithMany(r => r.Replies)
                   .HasForeignKey(r => r.ParentReviewId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Review>()
                   .HasOne(r => r.Book)
                   .WithMany(b => b.Reviews)
                   .HasForeignKey(r => r.BookId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Review>()
                   .HasOne(r => r.User)
                   .WithMany()
                   .HasForeignKey(r => r.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // timed discount
            builder.Entity<TimedDiscount>()
                   .HasOne(td => td.Book)
                   .WithMany()
                   .HasForeignKey(td => td.BookId)
                   .OnDelete(DeleteBehavior.Cascade);

            // announcement key
            builder.Entity<TimedAnnouncement>()
                   .HasKey(ta => ta.Id);

            // ignore Roles list on ApplicationUser
            builder.Entity<ApplicationUser>()
                   .Ignore(u => u.Roles);
        }
    }
}
