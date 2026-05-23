using Dapper;
using Microsoft.Data.Sqlite;

namespace AuctionArena.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=auction.db;Pooling=True";
            _logger = logger;
            InitializeDatabase();
        }

        public void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Create tables
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Lobbies (
                    LobbyId TEXT PRIMARY KEY,
                    HostName TEXT NOT NULL,
                    GameName TEXT NOT NULL,
                    Password TEXT,
                    PasswordHash TEXT,
                    PasswordSalt TEXT,
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
                    Version INTEGER DEFAULT 1,
                    FOREIGN KEY (LobbyId) REFERENCES Lobbies(LobbyId),
                    FOREIGN KEY (CurrentPlayerId) REFERENCES Players(PlayerId),
                    FOREIGN KEY (CurrentHighestBidderTeamId) REFERENCES Teams(TeamId)
                )
            ");

            // Create indexes for common queries
            CreateIndexIfNotExists(connection, "IX_Teams_LobbyId", "Teams(LobbyId)");
            CreateIndexIfNotExists(connection, "IX_Teams_LobbyId_OwnerName", "Teams(LobbyId, OwnerName)");
            CreateIndexIfNotExists(connection, "IX_Players_LobbyId", "Players(LobbyId)");
            CreateIndexIfNotExists(connection, "IX_Players_LobbyId_IsAuctioned", "Players(LobbyId, IsAuctioned)");
            CreateIndexIfNotExists(connection, "IX_Players_SoldToTeamId", "Players(SoldToTeamId)");
            CreateIndexIfNotExists(connection, "IX_Bids_PlayerId", "Bids(PlayerId)");
            CreateIndexIfNotExists(connection, "IX_Bids_LobbyId", "Bids(LobbyId)");

            // Migration: Add PasswordHash and PasswordSalt columns if they don't exist
            MigrateAddColumnIfNotExists(connection, "Lobbies", "PasswordHash", "TEXT");
            MigrateAddColumnIfNotExists(connection, "Lobbies", "PasswordSalt", "TEXT");
            MigrateAddColumnIfNotExists(connection, "AuctionState", "Version", "INTEGER DEFAULT 1");
            MigrateAddColumnIfNotExists(connection, "AuctionState", "TimerDuration", "INTEGER DEFAULT 30");

            _logger.LogInformation("Database initialized successfully with indexes");
        }

        private static void CreateIndexIfNotExists(SqliteConnection connection, string indexName, string definition)
        {
            try
            {
                connection.Execute($"CREATE INDEX IF NOT EXISTS {indexName} ON {definition}");
            }
            catch (Exception)
            {
                // Index might already exist or other non-critical error
            }
        }

        private static void MigrateAddColumnIfNotExists(SqliteConnection connection, string table, string column, string type)
        {
            try
            {
                var columns = connection.Query<string>($"PRAGMA table_info({table})").ToList();
                // Check if column exists
                var columnCheck = connection.Query($"SELECT * FROM pragma_table_info('{table}') WHERE name='{column}'");
                if (!columnCheck.Any())
                {
                    connection.Execute($"ALTER TABLE {table} ADD COLUMN {column} {type}");
                }
            }
            catch (Exception)
            {
                // Column might already exist
            }
        }
    }
}
