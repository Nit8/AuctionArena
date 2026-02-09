namespace AuctionArena.Models
{
    public class JoinLobbyViewModel
    {
        public string LobbyId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? Password { get; set; }
    }
}
