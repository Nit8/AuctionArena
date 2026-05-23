using AuctionArena.Models;

namespace AuctionArena.Interfaces
{
    public interface IAuctionService
    {
        // Lobby operations
        Task<(string LobbyId, string? Error)> CreateLobbyAsync(CreateLobbyViewModel model);
        Task<(Team? Team, string? Error)> ValidateJoinLobbyAsync(JoinLobbyViewModel model);
        Task<(bool Success, string? Error)> ValidateViewerAccessAsync(string lobbyId);

        // Auction flow
        Task<(bool Success, string? Error)> StartPlayerAuctionAsync(string lobbyId, int playerId);
        Task<(bool Success, string? Error)> PlaceBidAsync(string lobbyId, int playerId, int teamId, int bidAmount);
        Task<(bool Success, string? Error)> ConfirmSaleAsync(string lobbyId, int playerId);
        Task<(bool Success, string? Error)> SkipPlayerAsync(string lobbyId);
        Task<(bool Success, bool IsPaused, string? Error)> TogglePauseAsync(string lobbyId);
        Task<(bool Success, string? Error)> AddPointsAsync(string lobbyId, int teamId, int additionalPoints);

        // Enhanced host controls
        Task<(bool Success, string? Error)> RevokeSaleAsync(string lobbyId, int playerId);
        Task<(bool Success, string? Error)> ResetCurrentBidAsync(string lobbyId);
        Task<(bool Success, string? Error)> EndAuctionAsync(string lobbyId);
        Task<(bool Success, string? Error)> ReactivateAuctionAsync(string lobbyId);
        Task<(bool Success, string? Error)> SetTeamPointsAsync(int teamId, int points);
        Task<(bool Success, string? Error)> DeductTeamPointsAsync(string lobbyId, int teamId, int points);
        Task<(bool Success, int Duration, string? Error)> SetTimerDurationAsync(string lobbyId, int durationSeconds);

        // Player management
        Task<int> AddPlayerAsync(string lobbyId, string playerName, string position);
        Task<int> ImportPlayersAsync(string lobbyId, string playersData);
        Task DeletePlayerAsync(int playerId);

        // Dashboard data
        Task<AuctionViewModel> GetHostDashboardDataAsync(string lobbyId);
        Task<TeamDashboardViewModel> GetTeamDashboardDataAsync(string lobbyId, int teamId);
        Task<ViewerDashboardViewModel> GetViewerDashboardDataAsync(string lobbyId);
        Task<AuctionSummaryViewModel> GetAuctionSummaryAsync(string lobbyId);
        Task<List<Bid>> GetBidHistoryAsync(string lobbyId, int playerId);
    }
}
