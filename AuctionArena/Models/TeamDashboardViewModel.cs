namespace AuctionArena.Models
{
    public class TeamDashboardViewModel
    {
        public Team Team { get; set; } = new();
        public List<Player> MyPlayers { get; set; } = new();
        public Player? CurrentPlayer { get; set; }
        public int? CurrentHighestBid { get; set; }
        public string? CurrentHighestBidderName { get; set; }
        public int RemainingPoints { get; set; }
        public bool CanBid { get; set; }
        public bool IsPaused { get; set; }
    }
}
