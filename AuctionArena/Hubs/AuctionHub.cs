using Microsoft.AspNetCore.SignalR;

namespace AuctionArena.Hubs
{
    public class AuctionHub : Hub
    {
        private readonly ILogger<AuctionHub> _logger;

        public AuctionHub(ILogger<AuctionHub> logger)
        {
            _logger = logger;
        }

        public async Task JoinLobby(string lobbyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
            _logger.LogDebug("Connection {ConnectionId} joined lobby {LobbyId}", Context.ConnectionId, lobbyId);
        }

        public async Task LeaveLobby(string lobbyId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
            _logger.LogDebug("Connection {ConnectionId} left lobby {LobbyId}", Context.ConnectionId, lobbyId);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // Get current auction state for a lobby (used on reconnect)
        public async Task<object?> GetAuctionState(string lobbyId)
        {
            // This is handled by the HTTP API, but we provide a SignalR method for convenience
            await Task.CompletedTask;
            return null;
        }
    }
}
