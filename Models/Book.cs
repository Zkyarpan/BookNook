using System;
using System.Collections.Generic;
using BookNook.Models;

namespace BookHive.Models
{
    public class Book
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        private DateTime _addedDate;
        public DateTime AddedDate
        {
            get => _addedDate;
            set => _addedDate = EnsureUtc(value);
        }

        public DateTime PublicationDate { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string CoverImageUrl { get; set; } = string.Empty;

        public bool IsAvailable => Quantity > 0;
        public string ISBN { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public bool IsPhysicalLibraryAccess { get; set; }
        public bool IsBestseller { get; set; }
        public bool IsAwardWinner { get; set; }

        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => dt
            };
    }
}
