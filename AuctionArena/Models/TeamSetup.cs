namespace AuctionArena.Models
{
    public class TeamSetup
    {
        public string TeamName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? CaptainName { get; set; }
    }
}