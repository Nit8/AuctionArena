using System.ComponentModel.DataAnnotations;

namespace AuctionArena.Models
{
    public class ResumeLobbyViewModel
    {
        [Required(ErrorMessage = "Lobby code is required")]
        [StringLength(12, MinimumLength = 6, ErrorMessage = "Lobby code must be between 6 and 12 characters")]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Lobby code must contain only letters and numbers")]
        public string LobbyId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Host access key is required")]
        [StringLength(100, ErrorMessage = "Host access key must be at most 100 characters")]
        public string HostAccessKey { get; set; } = string.Empty;
    }
}