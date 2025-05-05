using System.ComponentModel.DataAnnotations;

namespace BookHive.Models
{
    public class Whitelist
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        [Required]
        public int BookId { get; set; }
        public Book Book { get; set; } = null!;
    }
}