using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookNook.Models   
{
    public class ApplicationUser : IdentityUser
    {
        public string? ProfileImageUrl { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        [NotMapped]                    
        public List<string> Roles { get; set; } = new List<string>();
    }
}
