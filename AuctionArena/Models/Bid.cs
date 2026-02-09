namespace AuctionArena.Models
{
    public class Bid
    {
        public int BidId { get; set; }
        public string LobbyId { get; set; } = string.Empty;
        public int PlayerId { get; set; }
        public int TeamId { get; set; }
        public int BidAmount { get; set; }
        public DateTime BidTime { get; set; }
    }
}
