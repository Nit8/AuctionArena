using AuctionArena.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AuctionArena.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=auction.db";
            InitializeDatabase();
        }

        private SqliteConnection GetConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public void InitializeDatabase()
        {
            using var connection = GetConnection();
            connection.Open();

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Lobbies (
                    LobbyId TEXT PRIMARY KEY,
                    HostName TEXT NOT NULL,
                    GameName TEXT NOT NULL,
                    Password TEXT,
                    TotalTeams INTEGER NOT NULL,
                    PlayersPerTeam INTEGER NOT NULL,
                    PointsPerTeam INTEGER NOT NULL,
                    MinPlayersPerTeam INTEGER NOT NULL,
                    MaxPlayersPerTeam INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    IsPaused INTEGER NOT NULL
                )
            ");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Teams (
                    TeamId INTEGER PRIMARY KEY AUTOINCREMENT,
                    LobbyId TEXT NOT NULL,
                    TeamName TEXT NOT NULL,
                    OwnerName TEXT NOT NULL,
                    CaptainName TEXT,
                    RemainingPoints INTEGER NOT NULL,
                    PlayerCount INTEGER NOT NULL,
                    FOREIGN KEY (LobbyId) REFERENCES Lobbies(LobbyId)
                )
            ");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Players (
                    PlayerId INTEGER PRIMARY KEY AUTOINCREMENT,
                    LobbyId TEXT NOT NULL,
                    PlayerName TEXT NOT NULL,
                    Position TEXT NOT NULL,
                    SoldToTeamId INTEGER,
                    SoldPrice INTEGER,
                    IsAuctioned INTEGER NOT NULL,
                    DisplayOrder INTEGER NOT NULL,
                    FOREIGN KEY (LobbyId) REFERENCES Lobbies(LobbyId),
                    FOREIGN KEY (SoldToTeamId) REFERENCES Teams(TeamId)
                )
            ");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Bids (
                    BidId INTEGER PRIMARY KEY AUTOINCREMENT,
                    LobbyId TEXT NOT NULL,
                    PlayerId INTEGER NOT NULL,
                    TeamId INTEGER NOT NULL,
                    BidAmount INTEGER NOT NULL,
                    BidTime TEXT NOT NULL,
                    FOREIGN KEY (LobbyId) REFERENCES Lobbies(LobbyId),
                    FOREIGN KEY (PlayerId) REFERENCES Players(PlayerId),
                    FOREIGN KEY (TeamId) REFERENCES Teams(TeamId)
                )
            ");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AuctionState (
                    LobbyId TEXT PRIMARY KEY,
                    CurrentPlayerId INTEGER,
                    CurrentHighestBid INTEGER,
                    CurrentHighestBidderTeamId INTEGER,
                    AuctionStartTime TEXT,
                    FOREIGN KEY (LobbyId) REFERENCES Lobbies(LobbyId),
                    FOREIGN KEY (CurrentPlayerId) REFERENCES Players(PlayerId),
                    FOREIGN KEY (CurrentHighestBidderTeamId) REFERENCES Teams(TeamId)
                )
            ");
        }

        // Lobby Operations
        public async Task<string> CreateLobby(Lobby lobby)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                INSERT INTO Lobbies (LobbyId, HostName, GameName, Password, TotalTeams, 
                    PlayersPerTeam, PointsPerTeam, MinPlayersPerTeam, MaxPlayersPerTeam, 
                    CreatedAt, IsActive, IsPaused)
                VALUES (@LobbyId, @HostName, @GameName, @Password, @TotalTeams, 
                    @PlayersPerTeam, @PointsPerTeam, @MinPlayersPerTeam, @MaxPlayersPerTeam, 
                    @CreatedAt, @IsActive, @IsPaused)
            ", lobby);
            return lobby.LobbyId;
        }

        public async Task<Lobby?> GetLobby(string lobbyId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Lobby>(
                "SELECT * FROM Lobbies WHERE LobbyId = @LobbyId", new { LobbyId = lobbyId });
        }

        public async Task UpdateLobbyPauseState(string lobbyId, bool isPaused)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Lobbies SET IsPaused = @IsPaused WHERE LobbyId = @LobbyId",
                new { LobbyId = lobbyId, IsPaused = isPaused });
        }

        // Team Operations
        public async Task<int> CreateTeam(Team team)
        {
            using var connection = GetConnection();
            return await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Teams (LobbyId, TeamName, OwnerName, CaptainName, RemainingPoints, PlayerCount)
                VALUES (@LobbyId, @TeamName, @OwnerName, @CaptainName, @RemainingPoints, @PlayerCount);
                SELECT last_insert_rowid();
            ", team);
        }

        public async Task<List<Team>> GetTeamsByLobby(string lobbyId)
        {
            using var connection = GetConnection();
            var teams = await connection.QueryAsync<Team>(
                "SELECT * FROM Teams WHERE LobbyId = @LobbyId ORDER BY TeamId",
                new { LobbyId = lobbyId });
            return teams.ToList();
        }

        public async Task<Team?> GetTeam(int teamId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Team>(
                "SELECT * FROM Teams WHERE TeamId = @TeamId", new { TeamId = teamId });
        }

        public async Task<Team?> GetTeamByOwnerName(string lobbyId, string ownerName)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Team>(
                "SELECT * FROM Teams WHERE LobbyId = @LobbyId AND OwnerName = @OwnerName",
                new { LobbyId = lobbyId, OwnerName = ownerName });
        }

        public async Task UpdateTeamPoints(int teamId, int remainingPoints)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Teams SET RemainingPoints = @RemainingPoints WHERE TeamId = @TeamId",
                new { TeamId = teamId, RemainingPoints = remainingPoints });
        }

        public async Task AddPointsToTeam(int teamId, int additionalPoints)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Teams SET RemainingPoints = RemainingPoints + @AdditionalPoints WHERE TeamId = @TeamId",
                new { TeamId = teamId, AdditionalPoints = additionalPoints });
        }

        public async Task UpdateTeamPlayerCount(int teamId, int playerCount)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(
                "UPDATE Teams SET PlayerCount = @PlayerCount WHERE TeamId = @TeamId",
                new { TeamId = teamId, PlayerCount = playerCount });
        }

        // Player Operations
        public async Task<int> CreatePlayer(Player player)
        {
            using var connection = GetConnection();
            return await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Players (LobbyId, PlayerName, Position, SoldToTeamId, SoldPrice, 
                    IsAuctioned, DisplayOrder)
                VALUES (@LobbyId, @PlayerName, @Position, @SoldToTeamId, @SoldPrice, 
                    @IsAuctioned, @DisplayOrder);
                SELECT last_insert_rowid();
            ", player);
        }

        public async Task<List<Player>> GetPlayersByLobby(string lobbyId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE LobbyId = @LobbyId ORDER BY DisplayOrder",
                new { LobbyId = lobbyId });
            return players.ToList();
        }

        public async Task<Player?> GetPlayer(int playerId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Player>(
                "SELECT * FROM Players WHERE PlayerId = @PlayerId", new { PlayerId = playerId });
        }

        public async Task<List<Player>> GetPlayersByTeam(int teamId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE SoldToTeamId = @TeamId ORDER BY SoldPrice DESC",
                new { TeamId = teamId });
            return players.ToList();
        }

        public async Task UpdatePlayerSold(int playerId, int teamId, int price)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                UPDATE Players 
                SET SoldToTeamId = @TeamId, SoldPrice = @Price, IsAuctioned = 1 
                WHERE PlayerId = @PlayerId",
                new { PlayerId = playerId, TeamId = teamId, Price = price });
        }

        public async Task<List<Player>> GetUnsoldPlayers(string lobbyId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE LobbyId = @LobbyId AND IsAuctioned = 0 ORDER BY DisplayOrder",
                new { LobbyId = lobbyId });
            return players.ToList();
        }

        public async Task<List<Player>> GetSoldPlayers(string lobbyId)
        {
            using var connection = GetConnection();
            var players = await connection.QueryAsync<Player>(
                "SELECT * FROM Players WHERE LobbyId = @LobbyId AND IsAuctioned = 1 ORDER BY SoldPrice DESC",
                new { LobbyId = lobbyId });
            return players.ToList();
        }

        // Bid Operations
        public async Task CreateBid(Bid bid)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                INSERT INTO Bids (LobbyId, PlayerId, TeamId, BidAmount, BidTime)
                VALUES (@LobbyId, @PlayerId, @TeamId, @BidAmount, @BidTime)
            ", bid);
        }

        public async Task<List<Bid>> GetBidsForPlayer(int playerId)
        {
            using var connection = GetConnection();
            var bids = await connection.QueryAsync<Bid>(
                "SELECT * FROM Bids WHERE PlayerId = @PlayerId ORDER BY BidAmount DESC",
                new { PlayerId = playerId });
            return bids.ToList();
        }

        // Auction State Operations
        public async Task UpdateAuctionState(AuctionState state)
        {
            using var connection = GetConnection();

            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM AuctionState WHERE LobbyId = @LobbyId",
                new { state.LobbyId });

            if (exists > 0)
            {
                await connection.ExecuteAsync(@"
                    UPDATE AuctionState 
                    SET CurrentPlayerId = @CurrentPlayerId, 
                        CurrentHighestBid = @CurrentHighestBid, 
                        CurrentHighestBidderTeamId = @CurrentHighestBidderTeamId,
                        AuctionStartTime = @AuctionStartTime
                    WHERE LobbyId = @LobbyId
                ", state);
            }
            else
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO AuctionState (LobbyId, CurrentPlayerId, CurrentHighestBid, 
                        CurrentHighestBidderTeamId, AuctionStartTime)
                    VALUES (@LobbyId, @CurrentPlayerId, @CurrentHighestBid, 
                        @CurrentHighestBidderTeamId, @AuctionStartTime)
                ", state);
            }
        }

        public async Task<AuctionState?> GetAuctionState(string lobbyId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<AuctionState>(
                "SELECT * FROM AuctionState WHERE LobbyId = @LobbyId",
                new { LobbyId = lobbyId });
        }

        public async Task ClearCurrentAuction(string lobbyId)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync(@"
                UPDATE AuctionState 
                SET CurrentPlayerId = NULL, 
                    CurrentHighestBid = NULL, 
                    CurrentHighestBidderTeamId = NULL,
                    AuctionStartTime = NULL
                WHERE LobbyId = @LobbyId
            ", new { LobbyId = lobbyId });
        }
    }
}
