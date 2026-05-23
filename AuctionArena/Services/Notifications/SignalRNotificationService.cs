using AuctionArena.Hubs;
using AuctionArena.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AuctionArena.Services.Notifications
{
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<AuctionHub> _hubContext;
        private readonly ILogger<SignalRNotificationService> _logger;

        public SignalRNotificationService(IHubContext<AuctionHub> hubContext, ILogger<SignalRNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyBidUpdate(string lobbyId, int playerId, int teamId, string teamName, int bidAmount)
        {
            _logger.LogInformation("Bid update: Lobby {LobbyId}, Player {PlayerId}, Team {TeamName} bid {Amount}", lobbyId, playerId, teamName, bidAmount);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveBidUpdate", new
            {
                playerId,
                teamId,
                teamName,
                bidAmount
            });
        }

        public async Task NotifyPlayerUpdate(string lobbyId, int? playerId, string? playerName, string? position)
        {
            _logger.LogInformation("Player update: Lobby {LobbyId}, Player {PlayerName}", lobbyId, playerName ?? "none");
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceivePlayerUpdate", new
            {
                playerId,
                playerName,
                position
            });
        }

        public async Task NotifyPlayerSold(string lobbyId, int playerId, string playerName, int teamId, string teamName, int soldPrice, string? position = null)
        {
            _logger.LogInformation("Player sold: Lobby {LobbyId}, {PlayerName} sold to {TeamName} for {Price}", lobbyId, playerName, teamName, soldPrice);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceivePlayerSold", new
            {
                playerId,
                playerName,
                teamId,
                teamName,
                soldPrice,
                position
            });
        }

        public async Task NotifyPauseUpdate(string lobbyId, bool isPaused)
        {
            _logger.LogInformation("Auction pause toggle: Lobby {LobbyId}, Paused={IsPaused}", lobbyId, isPaused);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceivePauseUpdate", isPaused);
        }

        public async Task NotifyTeamUpdate(string lobbyId, int teamId, string teamName, int? remainingPoints)
        {
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveTeamUpdate", new
            {
                teamId,
                teamName,
                remainingPoints
            });
        }

        public async Task NotifyAuctionComplete(string lobbyId, string message)
        {
            _logger.LogInformation("Auction complete: Lobby {LobbyId}", lobbyId);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveAuctionComplete", message);
        }

        public async Task NotifySaleRevoked(string lobbyId, int playerId, string playerName, int teamId, string teamName, int refundAmount)
        {
            _logger.LogInformation("Sale revoked: Lobby {LobbyId}, Player {PlayerName} returned from {TeamName}, refund {Refund}", lobbyId, playerName, teamName, refundAmount);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveSaleRevoked", new
            {
                playerId,
                playerName,
                teamId,
                teamName,
                refundAmount
            });
        }

        public async Task NotifyBidReset(string lobbyId, int playerId, string playerName, string position)
        {
            _logger.LogInformation("Bids reset: Lobby {LobbyId}, Player {PlayerName}", lobbyId, playerName);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveBidReset", new
            {
                playerId,
                playerName,
                position
            });
        }

        public async Task NotifyAuctionReactivated(string lobbyId)
        {
            _logger.LogInformation("Auction reactivated: Lobby {LobbyId}", lobbyId);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveAuctionReactivated");
        }

        public async Task NotifyTimerUpdate(string lobbyId, int durationSeconds)
        {
            _logger.LogInformation("Timer updated: Lobby {LobbyId}, Duration {Duration}s", lobbyId, durationSeconds);
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveTimerUpdate", durationSeconds);
        }
    }
}