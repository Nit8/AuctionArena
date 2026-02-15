using Microsoft.AspNetCore.SignalR;

namespace AuctionArena.Hubs
{
    public class AuctionHub : Hub
    {
        public async Task JoinLobby(string lobbyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
        }

        public async Task LeaveLobby(string lobbyId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
        }

        public async Task SendBidUpdate(string lobbyId, object bidData)
        {
            await Clients.Group(lobbyId).SendAsync("ReceiveBidUpdate", bidData);
        }

        public async Task SendPlayerUpdate(string lobbyId, object playerData)
        {
            await Clients.Group(lobbyId).SendAsync("ReceivePlayerUpdate", playerData);
        }

        public async Task SendAuctionStateUpdate(string lobbyId, object stateData)
        {
            await Clients.Group(lobbyId).SendAsync("ReceiveAuctionStateUpdate", stateData);
        }

        public async Task SendTeamUpdate(string lobbyId, object teamData)
        {
            await Clients.Group(lobbyId).SendAsync("ReceiveTeamUpdate", teamData);
        }

        public async Task SendPauseUpdate(string lobbyId, bool isPaused)
        {
            await Clients.Group(lobbyId).SendAsync("ReceivePauseUpdate", isPaused);
        }

        public async Task SendPlayerSold(string lobbyId, object soldData)
        {
            await Clients.Group(lobbyId).SendAsync("ReceivePlayerSold", soldData);
        }
    }
}
