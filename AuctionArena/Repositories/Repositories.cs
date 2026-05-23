using AuctionArena.Interfaces;
using AuctionArena.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace AuctionArena.Repositories
{
    public class LobbyRepository : ILobbyRepository
    {
        private readonly string _connectionString;

        public LobbyRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=auction.db";
        }

        private SqliteConnection GetConnection() => new(_connectionString);

        public async Task<string> CreateLobbyAsync(Lobby lobby)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                INSERT INTO Lobbies (LobbyId, HostName, GameName, Password, PasswordHash, PasswordSalt,
                    TotalTeams, PlayersPerTeam, PointsPerTeam, MinPlayersPerTeam, MaxPlayersPerTeam,
                    CreatedAt, IsActive, IsPaused)
                VALUES (@LobbyId, @HostName, @GameName, @Password, @PasswordHash, @PasswordSalt,
                    @TotalTeams, @PlayersPerTeam, @PointsPerTeam, @MinPlayersPerTeam, @MaxPlayersPerTeam,
                    @CreatedAt, @IsActive, @IsPaused)
            ", lobby);
            return lobby.LobbyId;
        }

        public async Task<Lobby?> GetLobbyAsync(string lobbyId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Lobby>(
                "SELECT * FROM Lobbies WHERE LobbyId = @LobbyId", new { LobbyId = lobbyId });
        }

        public async Task UpdateLobbyPauseStateAsync(string lobbyId, bool isPaused)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Lobbies SET IsPaused = @IsPaused WHERE LobbyId = @LobbyId",
                new { LobbyId = lobbyId, IsPaused = isPaused });
        }

        public async Task UpdateLobbyActiveStateAsync(string lobbyId, bool isActive)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Lobbies SET IsActive = @IsActive WHERE LobbyId = @LobbyId",
                new { LobbyId = lobbyId, IsActive = isActive });
        }

        public async Task DeactivateLobbyAsync(string lobbyId)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Lobbies SET IsActive = 0 WHERE LobbyId = @LobbyId",
                new { LobbyId = lobbyId });
        }
    }

    public class TeamRepository : ITeamRepository
    {
        private readonly string _connectionString;

        public TeamRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=auction.db";
        }

        private SqliteConnection GetConnection() => new(_connectionString);

        public async Task<int> CreateTeamAsync(Team team)
        {
            using var connection = GetConnection();
            return await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Teams (LobbyId, TeamName, OwnerName, CaptainName, RemainingPoints, PlayerCount)
                VALUES (@LobbyId, @TeamName, @OwnerName, @CaptainName, @RemainingPoints, @PlayerCount);
                SELECT last_insert_rowid();
            ", team);
        }

        public async Task<Team?> GetTeamAsync(int teamId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Team>(
                "SELECT * FROM Teams WHERE TeamId = @TeamId", new { TeamId = teamId });
        }

        public async Task<Team?> GetTeamByOwnerNameAsync(string lobbyId, string ownerName)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Team>(
                "SELECT * FROM Teams WHERE LobbyId = @LobbyId AND OwnerName = @OwnerName",
                new { LobbyId = lobbyId, OwnerName = ownerName });
        }

        public async Task<List<Team>> GetTeamsByLobbyAsync(string lobbyId)
        {
            using var connection = GetConnection();
            var teams = await connection.QueryAsync<Team>(
                "SELECT * FROM Teams WHERE LobbyId = @LobbyId ORDER BY TeamId",
                new { LobbyId = lobbyId });
            return teams.ToList();
        }

        public async Task UpdateTeamPointsAsync(int teamId, int remainingPoints)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Teams SET RemainingPoints = @RemainingPoints WHERE TeamId = @TeamId",
                new { TeamId = teamId, RemainingPoints = remainingPoints });
        }

        public async Task AddPointsToTeamAsync(int teamId, int additionalPoints)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Teams SET RemainingPoints = RemainingPoints + @AdditionalPoints WHERE TeamId = @TeamId",
                new { TeamId = teamId, AdditionalPoints = additionalPoints });
        }

        public async Task UpdateTeamPlayerCountAsync(int teamId, int playerCount)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Teams SET PlayerCount = @PlayerCount WHERE TeamId = @TeamId",
                new { TeamId = teamId, PlayerCount = playerCount });
        }

        public async Task DeductTeamPointsAsync(int teamId, int amount)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Teams SET RemainingPoints = RemainingPoints - @Amount WHERE TeamId = @TeamId AND RemainingPoints >= @Amount",
                new { TeamId = teamId, Amount = amount });
        }
    }

    public class PlayerRepository : IPlayerRepository
    {
        private readonly string _connectionString;

        public PlayerRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=auction.db";
        }

        private SqliteConnection GetConnection() => new(_connectionString);

        public async Task<int> CreatePlayerAsync(Player player)
        {
            using var connection = GetConnection();
            return await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Players (LobbyId, PlayerName, Position, SoldToTeamId, SoldPrice, IsAuctioned, DisplayOrder)
                VALUES (@LobbyId, @PlayerName, @Position, @SoldToTeamId, @SoldPrice, @IsAuctioned, @DisplayOrder);
                SELECT last_insert_rowid();
            ", player);
        }

        public async Task<Player?> GetPlayerAsync(int playerId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Player>(
                "SELECT * FROM Players WHERE PlayerId = @PlayerId", new { PlayerId = playerId });
        }

        public async Task<List<Player>> GetPlayersByLobbyAsync(string lobbyId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE LobbyId = @LobbyId ORDER BY DisplayOrder",
                new { LobbyId = lobbyId });
            return players.ToList();
        }

        public async Task<List<Player>> GetPlayersByTeamAsync(int teamId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE SoldToTeamId = @TeamId ORDER BY SoldPrice DESC",
                new { TeamId = teamId });
            return players.ToList();
        }

        public async Task<List<Player>> GetUnsoldPlayersAsync(string lobbyId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE LobbyId = @LobbyId AND IsAuctioned = 0 ORDER BY DisplayOrder",
                new { LobbyId = lobbyId });
            return players.ToList();
        }

        public async Task<List<Player>> GetSoldPlayersAsync(string lobbyId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE LobbyId = @LobbyId AND IsAuctioned = 1 ORDER BY SoldPrice DESC",
                new { LobbyId = lobbyId });
            return players.ToList();
        }

        public async Task UpdatePlayerSoldAsync(int playerId, int teamId, int price)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                UPDATE Players SET SoldToTeamId = @TeamId, SoldPrice = @Price, IsAuctioned = 1
                WHERE PlayerId = @PlayerId",
                new { PlayerId = playerId, TeamId = teamId, Price = price });
        }

        public async Task DeletePlayerAsync(int playerId)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "DELETE FROM Players WHERE PlayerId = @PlayerId AND IsAuctioned = 0",
                new { PlayerId = playerId });
        }

        public async Task UpdatePlayerAsync(Player player)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                UPDATE Players SET PlayerName = @PlayerName, Position = @Position
                WHERE PlayerId = @PlayerId AND IsAuctioned = 0",
                player);
        }
    }

    public class BidRepository : IBidRepository
    {
        private readonly string _connectionString;

        public BidRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=auction.db";
        }

        private SqliteConnection GetConnection() => new(_connectionString);

        public async Task CreateBidAsync(Bid bid)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                INSERT INTO Bids (LobbyId, PlayerId, TeamId, BidAmount, BidTime)
                VALUES (@LobbyId, @PlayerId, @TeamId, @BidAmount, @BidTime)
            ", bid);
        }

        public async Task<List<Bid>> GetBidsForPlayerAsync(int playerId)
        {
            using var connection = GetConnection();
            var bids = await connection.QueryAsync<Bid>(
                "SELECT * FROM Bids WHERE PlayerId = @PlayerId ORDER BY BidAmount DESC",
                new { PlayerId = playerId });
            return bids.ToList();
        }

        public async Task<List<Bid>> GetBidsForLobbyAsync(string lobbyId)
        {
            using var connection = GetConnection();
            var bids = await connection.QueryAsync<Bid>(
                "SELECT * FROM Bids WHERE LobbyId = @LobbyId ORDER BY BidTime DESC",
                new { LobbyId = lobbyId });
            return bids.ToList();
        }
    }

    public class AuctionStateRepository : IAuctionStateRepository
    {
        private readonly string _connectionString;

        public AuctionStateRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=auction.db";
        }

        private SqliteConnection GetConnection() => new(_connectionString);

        public async Task<AuctionState?> GetAuctionStateAsync(string lobbyId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<AuctionState>(
                "SELECT * FROM AuctionState WHERE LobbyId = @LobbyId",
                new { LobbyId = lobbyId });
        }

        public async Task UpdateAuctionStateAsync(AuctionState state)
        {
            using var connection = GetConnection();
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM AuctionState WHERE LobbyId = @LobbyId",
                new { state.LobbyId });

            if (exists > 0)
            {
                var rowsAffected = await connection.ExecuteAsync(@"
                    UPDATE AuctionState 
                    SET CurrentPlayerId = @CurrentPlayerId, 
                        CurrentHighestBid = @CurrentHighestBid, 
                        CurrentHighestBidderTeamId = @CurrentHighestBidderTeamId,
                        AuctionStartTime = @AuctionStartTime,
                        Version = Version + 1
                    WHERE LobbyId = @LobbyId AND Version = @Version
                ", state);

                if (rowsAffected == 0)
                {
                    var currentVersion = await connection.ExecuteScalarAsync<int?>(
                        "SELECT Version FROM AuctionState WHERE LobbyId = @LobbyId",
                        new { state.LobbyId });
                    if (currentVersion.HasValue)
                    {
                        state.Version = currentVersion.Value;
                        await connection.ExecuteAsync(@"
                UPDATE AuctionState 
                SET CurrentPlayerId = @CurrentPlayerId, 
                    CurrentHighestBid = @CurrentHighestBid, 
                    CurrentHighestBidderTeamId = @CurrentHighestBidderTeamId,
                    AuctionStartTime = @AuctionStartTime,
                    Version = Version + 1
                WHERE LobbyId = @LobbyId AND Version = @Version
            ", state);
                    }
                }
            }
            else
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO AuctionState (LobbyId, CurrentPlayerId, CurrentHighestBid, 
                        CurrentHighestBidderTeamId, AuctionStartTime, Version)
                    VALUES (@LobbyId, @CurrentPlayerId, @CurrentHighestBid, 
                        @CurrentHighestBidderTeamId, @AuctionStartTime, 1)
                ", state);
            }
        }

        public async Task ClearCurrentAuctionAsync(string lobbyId)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                UPDATE AuctionState 
                SET CurrentPlayerId = NULL, 
                    CurrentHighestBid = NULL, 
                    CurrentHighestBidderTeamId = NULL,
                    AuctionStartTime = NULL,
                    Version = Version + 1
                WHERE LobbyId = @LobbyId
            ", new { LobbyId = lobbyId });
        }
    }
}
