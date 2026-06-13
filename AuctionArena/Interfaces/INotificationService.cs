using AuctionArena.Models;

namespace AuctionArena.Interfaces
{
    public interface INotificationService
    {
        Task NotifyBidUpdate(string lobbyId, int playerId, int teamId, string teamName, int bidAmount);
        Task NotifyPlayerUpdate(string lobbyId, int? playerId, string? playerName, string? position);
        Task NotifyPlayerSold(string lobbyId, int playerId, string playerName, int teamId, string teamName, int soldPrice, string? position = null);
        Task NotifyPauseUpdate(string lobbyId, bool isPaused);
        Task NotifyTeamUpdate(string lobbyId, int teamId, string teamName, int? remainingPoints);
        Task NotifyAuctionComplete(string lobbyId, string message);
        Task NotifySaleRevoked(string lobbyId, int playerId, string playerName, int teamId, string teamName, int refundAmount);
        Task NotifyBidReset(string lobbyId, int playerId, string playerName, string position);
        Task NotifyAuctionReactivated(string lobbyId);
        Task NotifyTimerUpdate(string lobbyId, int durationSeconds);
        Task NotifyAvailablePlayersUpdate(string lobbyId, List<Player> players);
        Task NotifyTeamSuspension(string lobbyId, int teamId, string teamName, bool isSuspended);
        Task NotifyBidIncrementUpdate(string lobbyId, int bidIncrement);
    }
}