using System.ComponentModel.DataAnnotations;

namespace AuctionArena.Models
{
    public class TeamSetup
    {
        [Required(ErrorMessage = "Team name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Team name must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_.]+$", ErrorMessage = "Team name contains invalid characters")]
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Owner name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Owner name must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_.]+$", ErrorMessage = "Owner name contains invalid characters")]
        public string OwnerName { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Captain name must be at most 50 characters")]
        public string? CaptainName { get; set; }
    }
}
