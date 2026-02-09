namespace AuctionArena.Models
{
    public class AuctionViewModel
    {
        public Lobby Lobby { get; set; } = new();
        public List<Team> Teams { get; set; } = new();
        public Player? CurrentPlayer { get; set; }
        public int? CurrentHighestBid { get; set; }
        public Team? CurrentHighestBidder { get; set; }
        public List<Player> RemainingPlayers { get; set; } = new();
        public List<Player> SoldPlayers { get; set; } = new();
        public bool IsPaused { get; set; }
    }
}
