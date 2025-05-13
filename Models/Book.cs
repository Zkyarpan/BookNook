using System;
using System.Collections.Generic;
using BookNook.Models;

namespace BookNook.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Genre { get; set; }
        public string Description { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.UtcNow;
        public DateTime PublicationDate { get; set; } = DateTime.UtcNow;

        public decimal Price { get; set; }
        public int Quantity { get; set; }

        public string? CoverImageUrl { get; set; }

        public bool IsAvailable => Quantity > 0;
        public string ISBN { get; set; }
        public string Language { get; set; }
        public string Format { get; set; }
        public string Publisher { get; set; }
        public bool IsPhysicalLibraryAccess { get; set; }
        public bool IsBestseller { get; set; }
        public bool IsAwardWinner { get; set; }
        public bool IsComingSoon { get; set; }
        public DateTime? ReleaseDate { get; set; }


        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}