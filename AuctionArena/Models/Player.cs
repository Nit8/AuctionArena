namespace AuctionArena.Models
{
    public class Player
    {
        public int PlayerId { get; set; }
        public string LobbyId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int? SoldToTeamId { get; set; }
        public int? SoldPrice { get; set; }
        public bool IsAuctioned { get; set; }
        public int DisplayOrder { get; set; }
    }
}
