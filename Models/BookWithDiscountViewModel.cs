using System.Collections.Generic;
using BookHive.Models;

namespace BookNook.Models
{
    public class BookWithDiscountViewModel
    {
        public Book? Book { get; set; }                       // the book itself
        public bool OnSaleFlag { get; set; }            // banner / badge indicator
        public bool IsDiscountActive { get; set; }           // is a timed‑discount currently active?
        public decimal DiscountedPrice { get; set; }          // price after discount (or original if no discount)

        // populated only on detail pages where reviews are needed
        public List<Review> Reviews { get; set; } = new();
    }
}
