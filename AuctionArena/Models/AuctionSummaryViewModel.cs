namespace AuctionArena.Models
{
    public class AuctionSummaryViewModel
    {
        public Lobby Lobby { get; set; } = new();
        public List<TeamSummary> TeamSummaries { get; set; } = new();
        public List<PlayerSummaryRow> SoldPlayers { get; set; } = new();
        public List<PlayerSummaryRow> UnsoldPlayers { get; set; } = new();
        public List<MostExpensivePlayer> MostExpensivePlayers { get; set; } = new();
        public List<PositionBreakdown> PositionBreakdowns { get; set; } = new();
        public int TotalPlayers { get; set; }
        public int SoldCount { get; set; }
        public int UnsoldCount { get; set; }
        public int TotalPointsSpent { get; set; }
        public int TotalPointsRemaining { get; set; }
        public double AverageSoldPrice { get; set; }
        public int HighestSoldPrice { get; set; }
        public string HighestSoldPlayer { get; set; } = string.Empty;
        public string HighestSoldTeam { get; set; } = string.Empty;
        public TeamSummary? HighestBidderTeam { get; set; }
        public TeamSummary? MostEconomicalTeam { get; set; }
        public TeamSummary? MostPlayersTeam { get; set; }
    }

    public class TeamSummary
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? CaptainName { get; set; }
        public int InitialPoints { get; set; }
        public int RemainingPoints { get; set; }
        public int TotalSpent { get; set; }
        public int PlayerCount { get; set; }
        public double SpendPercentage { get; set; }
        public double AvgPricePerPlayer { get; set; }
        public int MaxPlayersAllowed { get; set; }
        public List<PlayerSummaryRow> Players { get; set; } = new();
    }

    public class PlayerSummaryRow
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int? SoldPrice { get; set; }
        public string? SoldToTeam { get; set; }
        public int? SoldToTeamId { get; set; }
        public bool IsSold { get; set; }
    }

    public class MostExpensivePlayer
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int SoldPrice { get; set; }
        public string SoldToTeam { get; set; } = string.Empty;
        public int SoldToTeamId { get; set; }
    }

    public class PositionBreakdown
    {
        public string Position { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int SoldCount { get; set; }
        public int UnsoldCount { get; set; }
        public int TotalSpent { get; set; }
        public double AveragePrice { get; set; }
        public int HighestPrice { get; set; }
    }
}
