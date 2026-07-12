namespace AuctionArena.Models
{
    public class TeamDashboardViewModel
    {
        public Team Team { get; set; } = new();
        public List<Team> AllTeams { get; set; } = new();
        public List<Player> MyPlayers { get; set; } = new();
        public Player? CurrentPlayer { get; set; }
        public int? CurrentHighestBid { get; set; }
        public string? CurrentHighestBidderName { get; set; }
        public int? MinimumBid { get; set; } // Host-defined minimum starting bid for the current player
        public int RemainingPoints { get; set; }
        public bool CanBid { get; set; }
        public bool IsPaused { get; set; }
        public int MaxPlayersPerTeam { get; set; }
        public int CurrentPlayerCount { get; set; }
        public List<Bid> RecentBids { get; set; } = new();
        public List<Player> AvailablePlayers { get; set; } = new();
    }
}
