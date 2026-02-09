namespace AuctionArena.Models
{
    public class Team
    {
        public int TeamId { get; set; }
        public string LobbyId { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? CaptainName { get; set; }
        public int RemainingPoints { get; set; }
        public int PlayerCount { get; set; }
    }
}
