using System.ComponentModel.DataAnnotations;

namespace AuctionArena.Models
{
    public class ResumeLobbyViewModel
    {
        [Required(ErrorMessage = "Lobby code is required")]
        [StringLength(12, MinimumLength = 6, ErrorMessage = "Lobby code must be between 6 and 12 characters")]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Lobby code must contain only letters and numbers")]
        public string LobbyId { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Password must be at most 100 characters")]
        public string? Password { get; set; }
    }
}
