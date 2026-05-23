namespace AuctionArena.Models
{
    public class ViewerDashboardViewModel
    {
        public Lobby Lobby { get; set; } = new();
        public List<Team> Teams { get; set; } = new();
        public Player? CurrentPlayer { get; set; }
        public int? CurrentHighestBid { get; set; }
        public Team? CurrentHighestBidder { get; set; }
        public List<Player> RemainingPlayers { get; set; } = new();
        public List<Player> SoldPlayers { get; set; } = new();
        public List<Bid> RecentBids { get; set; } = new();
        public List<Bid> CurrentBids { get; set; } = new();
        public bool IsPaused { get; set; }
        public bool IsActive { get; set; }
        public int TimerDuration { get; set; } = 30;
        public int TotalSpent { get; set; }
    }
}
