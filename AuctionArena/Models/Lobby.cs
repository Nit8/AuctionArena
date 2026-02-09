namespace AuctionArena.Models
{
    public class Lobby
    {
        public string LobbyId { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string? Password { get; set; }
        public int TotalTeams { get; set; }
        public int PlayersPerTeam { get; set; }
        public int PointsPerTeam { get; set; }
        public int MinPlayersPerTeam { get; set; }
        public int MaxPlayersPerTeam { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsPaused { get; set; }
    }
}
