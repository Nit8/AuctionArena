using AuctionArena.Models;

namespace AuctionArena.Interfaces
{
    public interface ILobbyRepository
    {
        Task<string> CreateLobbyAsync(Lobby lobby);
        Task<Lobby?> GetLobbyAsync(string lobbyId);
        Task UpdateLobbyPauseStateAsync(string lobbyId, bool isPaused);
        Task UpdateLobbyActiveStateAsync(string lobbyId, bool isActive);
        Task DeactivateLobbyAsync(string lobbyId);
    }

    public interface ITeamRepository
    {
        Task<int> CreateTeamAsync(Team team);
        Task<Team?> GetTeamAsync(int teamId);
        Task<Team?> GetTeamByOwnerNameAsync(string lobbyId, string ownerName);
        Task<List<Team>> GetTeamsByLobbyAsync(string lobbyId);
        Task UpdateTeamPointsAsync(int teamId, int remainingPoints);
        Task AddPointsToTeamAsync(int teamId, int additionalPoints);
        Task UpdateTeamPlayerCountAsync(int teamId, int playerCount);
        Task DeductTeamPointsAsync(int teamId, int amount);
    }

    public interface IPlayerRepository
    {
        Task<int> CreatePlayerAsync(Player player);
        Task<Player?> GetPlayerAsync(int playerId);
        Task<List<Player>> GetPlayersByLobbyAsync(string lobbyId);
        Task<List<Player>> GetPlayersByTeamAsync(int teamId);
        Task<List<Player>> GetUnsoldPlayersAsync(string lobbyId);
        Task<List<Player>> GetSoldPlayersAsync(string lobbyId);
        Task UpdatePlayerSoldAsync(int playerId, int teamId, int price);
        Task DeletePlayerAsync(int playerId);
        Task UpdatePlayerAsync(Player player);
    }

    public interface IBidRepository
    {
        Task CreateBidAsync(Bid bid);
        Task<List<Bid>> GetBidsForPlayerAsync(int playerId);
        Task<List<Bid>> GetBidsForLobbyAsync(string lobbyId);
    }

    public interface IAuctionStateRepository
    {
        Task<AuctionState?> GetAuctionStateAsync(string lobbyId);
        Task UpdateAuctionStateAsync(AuctionState state);
        Task ClearCurrentAuctionAsync(string lobbyId);
    }
}
