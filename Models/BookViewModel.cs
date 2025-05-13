using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BookNook.Models
{
    public class BookViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title can't exceed 200 characters.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Author is required.")]
        [StringLength(100, ErrorMessage = "Author name can't exceed 100 characters.")]
        public string Author { get; set; }

        [Required(ErrorMessage = "Genre is required.")]
        [StringLength(100, ErrorMessage = "Genre can't exceed 100 characters.")]
        public string Genre { get; set; }

        [StringLength(1000, ErrorMessage = "Description can't exceed 1000 characters.")]
        public string Description { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Publication Date")]
        [Range(typeof(DateTime), "1900-01-01", "2025-01-01", ErrorMessage = "Publication Date must be between 1900 and 2025.")]
        public DateTime? PublicationDate { get; set; }

        [Range(0.01, 10000, ErrorMessage = "Price must be between 0.01 and 10,000.")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
        public int Quantity { get; set; }

        public string? CoverImageUrl { get; set; }

        [Display(Name = "Cover Image")]
        // Optional: do NOT add [Required] here. The controller handles conditional validation.
        public IFormFile? CoverImage { get; set; }

        [StringLength(13, ErrorMessage = "ISBN can't exceed 13 characters.")]
        public string ISBN { get; set; }

        [StringLength(50, ErrorMessage = "Language can't exceed 50 characters.")]
        public string Language { get; set; }

        [StringLength(50, ErrorMessage = "Format can't exceed 50 characters.")]
        public string Format { get; set; }

        [StringLength(100, ErrorMessage = "Publisher can't exceed 100 characters.")]
        public string Publisher { get; set; }

        public bool IsPhysicalLibraryAccess { get; set; }
        public bool IsBestseller { get; set; }
        public bool IsAwardWinner { get; set; }
        public bool IsComingSoon { get; set; }

        [Display(Name = "Release Date")]
        [DataType(DataType.Date)]
        public DateTime? ReleaseDate { get; set; }

    }
}