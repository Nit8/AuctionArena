namespace AuctionArena.Models
{
    public class AuctionState
    {
        public string LobbyId { get; set; } = string.Empty;
        public int? CurrentPlayerId { get; set; }
        public int? CurrentHighestBid { get; set; }
        public int? CurrentHighestBidderTeamId { get; set; }
        public DateTime? AuctionStartTime { get; set; }
    }
}
