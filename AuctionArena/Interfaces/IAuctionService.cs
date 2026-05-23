using AuctionArena.Models;

namespace AuctionArena.Interfaces
{
    public interface IAuctionService
    {
        // Lobby operations
        Task<(string LobbyId, string? Error)> CreateLobbyAsync(CreateLobbyViewModel model);
        Task<(Team? Team, string? Error)> ValidateJoinLobbyAsync(JoinLobbyViewModel model);

        // Auction flow
        Task<(bool Success, string? Error)> StartPlayerAuctionAsync(string lobbyId, int playerId);
        Task<(bool Success, string? Error)> PlaceBidAsync(string lobbyId, int playerId, int teamId, int bidAmount);
        Task<(bool Success, string? Error)> ConfirmSaleAsync(string lobbyId, int playerId);
        Task<(bool Success, string? Error)> SkipPlayerAsync(string lobbyId);
        Task<(bool Success, bool IsPaused, string? Error)> TogglePauseAsync(string lobbyId);
        Task<(bool Success, string? Error)> AddPointsAsync(string lobbyId, int teamId, int additionalPoints);

        // Player management
        Task<int> AddPlayerAsync(string lobbyId, string playerName, string position);
        Task<int> ImportPlayersAsync(string lobbyId, string playersData);
        Task DeletePlayerAsync(int playerId);

        // Dashboard data
        Task<AuctionViewModel> GetHostDashboardDataAsync(string lobbyId);
        Task<TeamDashboardViewModel> GetTeamDashboardDataAsync(string lobbyId, int teamId);
        Task<List<Bid>> GetBidHistoryAsync(string lobbyId, int playerId);
    }
}
