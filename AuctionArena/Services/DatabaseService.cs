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
            ",lobby);
            return lobby.LobbyId;
        }
        public async Task<Lobby?> GetLobby(string lobbyId)
        {
            using var connection = GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Lobby>(
                "SELECT * FROM Lobbies WHERE LobbyId = @LobbyId", new { LobbyId = lobbyId });
        }

    }
}
