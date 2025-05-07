using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BookNook.Models
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "First Name is required.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required.")]
        public string LastName { get; set; } = string.Empty;

        public string? ProfilePictureUrl { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }
    }
}
