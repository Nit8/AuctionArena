using System.ComponentModel.DataAnnotations;

namespace AuctionArena.Models
{
    public class JoinLobbyViewModel
    {
        [Required(ErrorMessage = "Lobby code is required")]
        [StringLength(12, MinimumLength = 6, ErrorMessage = "Lobby code must be between 6 and 12 characters")]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Lobby code must contain only letters and numbers")]
        public string LobbyId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Owner name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Owner name must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_.]+$", ErrorMessage = "Owner name contains invalid characters")]
        public string OwnerName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Password must be at most 100 characters")]
        public string? Password { get; set; }
    }
}
