using Microsoft.AspNetCore.SignalR;

namespace AuctionApp.Hubs
{
    public class AuctionHub : Hub
    {
        // Client joins a lobby group
        public async Task JoinLobby(string lobbyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
        }

        // Send bid update to everyone in lobby
        public async Task SendBidUpdate(string lobbyId, object bidData)
        {
            await Clients.Group(lobbyId).SendAsync("ReceiveBidUpdate", bidData);
        }
    }
}