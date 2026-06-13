using AuctionArena.Interfaces;
using AuctionArena.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace AuctionArena.Services
{
    public class AuctionService : IAuctionService
    {
        private readonly ILobbyRepository _lobbyRepo;
        private readonly ITeamRepository _teamRepo;
        private readonly IPlayerRepository _playerRepo;
        private readonly IBidRepository _bidRepo;
        private readonly IAuctionStateRepository _auctionStateRepo;
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuctionService> _logger;
        private readonly string _connectionString;

        public AuctionService(
            ILobbyRepository lobbyRepo,
            ITeamRepository teamRepo,
            IPlayerRepository playerRepo,
            IBidRepository bidRepo,
            IAuctionStateRepository auctionStateRepo,
            INotificationService notificationService,
            IConfiguration configuration,
            ILogger<AuctionService> logger)
        {
            _lobbyRepo = lobbyRepo;
            _teamRepo = teamRepo;
            _playerRepo = playerRepo;
            _bidRepo = bidRepo;
            _auctionStateRepo = auctionStateRepo;
            _notificationService = notificationService;
            _configuration = configuration;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=auction.db";
        }

        // ─── Password Hashing ───
        public static (string Hash, string Salt) HashPassword(string password)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(16);
            var salt = Convert.ToBase64String(saltBytes);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
            return (Convert.ToBase64String(hash), salt);
        }

        public static bool VerifyPassword(string password, string hash, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
            return Convert.ToBase64String(computedHash) == hash;
        }

        // ─── Lobby ID Generation (collision-safe) ───
        private static string GenerateLobbyId()
        {
            var bytes = RandomNumberGenerator.GetBytes(9); // 12 chars in Base64Url
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").ToUpperInvariant();
        }

        // ─── Host Access Key Generation ───
        private static string GenerateHostAccessKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(24);
            return Convert.ToBase64String(bytes)
                .Replace("+", "").Replace("/", "").Replace("=", "");
        }

        // ─── Lobby Operations ───
        public async Task<(string LobbyId, string? Error)> CreateLobbyAsync(CreateLobbyViewModel model)
        {
            var lobbyId = GenerateLobbyId();

            // Ensure no collision
            while (await _lobbyRepo.GetLobbyAsync(lobbyId) != null)
            {
                lobbyId = GenerateLobbyId();
            }

            string? passwordHash = null;
            string? passwordSalt = null;
            string hostAccessKey = GenerateHostAccessKey();
            if (!string.IsNullOrEmpty(model.Password))
            {
                (passwordHash, passwordSalt) = HashPassword(model.Password);
            }

            var lobby = new Lobby
            {
                LobbyId = lobbyId,
                HostName = model.HostName,
                GameName = model.GameName,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Password = null, // Don't store plaintext
                HostAccessKey = hostAccessKey,
                TotalTeams = model.TotalTeams,
                PlayersPerTeam = model.PlayersPerTeam,
                PointsPerTeam = model.PointsPerTeam,
                MinPlayersPerTeam = model.MinPlayersPerTeam,
                MaxPlayersPerTeam = model.MaxPlayersPerTeam,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPaused = false
            };

            await _lobbyRepo.CreateLobbyAsync(lobby);

            foreach (var teamSetup in model.Teams)
            {
                var team = new Team
                {
                    LobbyId = lobbyId,
                    TeamName = teamSetup.TeamName.Trim(),
                    OwnerName = teamSetup.OwnerName.Trim(),
                    CaptainName = teamSetup.CaptainName?.Trim(),
                    RemainingPoints = model.PointsPerTeam,
                    PlayerCount = 0
                };
                await _teamRepo.CreateTeamAsync(team);
            }

            _logger.LogInformation("Lobby created: {LobbyId} by {HostName} for {GameName}", lobbyId, model.HostName, model.GameName);
            return (lobbyId, null);
        }

        public async Task<(Team? Team, string? Error)> ValidateJoinLobbyAsync(JoinLobbyViewModel model)
        {
            var lobby = await _lobbyRepo.GetLobbyAsync(model.LobbyId.ToUpperInvariant());
            if (lobby == null)
                return (null, "Lobby not found");

            if (!lobby.IsActive)
                return (null, "This lobby is no longer active");

            // Verify password
            if (!string.IsNullOrEmpty(lobby.PasswordHash) && !string.IsNullOrEmpty(lobby.PasswordSalt))
            {
                if (string.IsNullOrEmpty(model.Password) || !VerifyPassword(model.Password, lobby.PasswordHash, lobby.PasswordSalt))
                    return (null, "Incorrect password");
            }
            else if (!string.IsNullOrEmpty(lobby.Password))
            {
                // Legacy plaintext support for migration
#pragma warning disable CS0618
                if (lobby.Password != model.Password)
                    return (null, "Incorrect password");
#pragma warning restore CS0618
            }

            var team = await _teamRepo.GetTeamByOwnerNameAsync(model.LobbyId.ToUpperInvariant(), model.OwnerName.Trim());
            if (team == null)
                return (null, "You are not registered in this lobby");

            _logger.LogInformation("Team owner joined: {OwnerName} in lobby {LobbyId}", model.OwnerName, model.LobbyId);
            return (team, null);
        }

        public async Task<(string LobbyId, string? Error)> ValidateResumeLobbyAsync(ResumeLobbyViewModel model)
        {
            var lobbyId = model.LobbyId.ToUpperInvariant().Trim();
            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
            if (lobby == null)
                return (string.Empty, "Lobby not found. Check the lobby code and try again.");

            // Verify host access key
            if (string.IsNullOrEmpty(lobby.HostAccessKey))
                return (string.Empty, "This lobby does not support resume. Host access key was not generated.");

            if (string.IsNullOrEmpty(model.HostAccessKey) || lobby.HostAccessKey != model.HostAccessKey.Trim())
                return (string.Empty, "Invalid host access key");

            _logger.LogInformation("Host resumed lobby {LobbyId}", lobbyId);
            return (lobbyId, null);
        }

        // ─── Auction Flow (with transaction support for concurrency) ───
        public async Task<(bool Success, string? Error)> StartPlayerAuctionAsync(string lobbyId, int playerId)
        {
            var player = await _playerRepo.GetPlayerAsync(playerId);
            if (player == null || player.IsAuctioned)
                return (false, "Player not available for auction");

            if (player.LobbyId != lobbyId)
                return (false, "Player does not belong to this lobby");

            // Read existing state to get the correct Version for optimistic concurrency
            var existingState = await _auctionStateRepo.GetAuctionStateAsync(lobbyId);

            var auctionState = new AuctionState
            {
                LobbyId = lobbyId,
                CurrentPlayerId = playerId,
                CurrentHighestBid = null,
                CurrentHighestBidderTeamId = null,
                AuctionStartTime = DateTime.UtcNow,
                Version = existingState?.Version ?? 0
            };

            await _auctionStateRepo.UpdateAuctionStateAsync(auctionState);

            await _notificationService.NotifyPlayerUpdate(lobbyId, player.PlayerId, player.PlayerName, player.Position);

            // Notify all clients of the updated available players list (excludes the now-in-auction player)
            var updatedAvailable = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
            await _notificationService.NotifyAvailablePlayersUpdate(lobbyId, updatedAvailable);

            _logger.LogInformation("Auction started for player {PlayerName} (ID:{PlayerId}) in lobby {LobbyId}", player.PlayerName, playerId, lobbyId);
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> PlaceBidAsync(string lobbyId, int playerId, int teamId, int bidAmount)
        {
            try
            {
                var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
                if (lobby == null || lobby.IsPaused)
                    return (false, "Auction is paused or lobby not found");

                var team = await _teamRepo.GetTeamAsync(teamId);
                if (team == null)
                    return (false, "Team not found");

                var auctionState = await _auctionStateRepo.GetAuctionStateAsync(lobbyId);
                if (auctionState?.CurrentPlayerId != playerId)
                    return (false, "This player is not currently in auction");

                if (bidAmount <= 0)
                    return (false, "Bid amount must be positive");

                if (auctionState.CurrentHighestBid != null && bidAmount <= auctionState.CurrentHighestBid)
                    return (false, "Bid must be higher than current bid");

                if (bidAmount > team.RemainingPoints)
                    return (false, "Insufficient points");

                if (team.PlayerCount >= lobby.MaxPlayersPerTeam)
                    return (false, "Team has reached maximum players");

                if (team.IsSuspended)
                    return (false, "Your team has been suspended from bidding. Contact the host.");

                if (auctionState.CurrentHighestBidderTeamId == teamId)
                    return (false, "You already have the highest bid");

                // Enforce bid increment
                if (lobby.BidIncrement > 0 && auctionState.CurrentHighestBid != null)
                {
                    var minimumBid = auctionState.CurrentHighestBid.Value + lobby.BidIncrement;
                    if (bidAmount < minimumBid)
                        return (false, $"Bid must be at least {minimumBid} (minimum increment: {lobby.BidIncrement})");
                }

                // Create bid
                var bid = new Bid
                {
                    LobbyId = lobbyId,
                    PlayerId = playerId,
                    TeamId = teamId,
                    BidAmount = bidAmount,
                    BidTime = DateTime.UtcNow
                };
                await _bidRepo.CreateBidAsync(bid);

                // Update auction state with optimistic concurrency
                auctionState.CurrentHighestBid = bidAmount;
                auctionState.CurrentHighestBidderTeamId = teamId;
                await _auctionStateRepo.UpdateAuctionStateAsync(auctionState);

                // Notify after successful commit
                await _notificationService.NotifyBidUpdate(lobbyId, playerId, teamId, team.TeamName, bidAmount);

                _logger.LogInformation("Bid placed: Team {TeamName} bid {Amount} on Player {PlayerId} in lobby {LobbyId}",
                    team.TeamName, bidAmount, playerId, lobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing bid in lobby {LobbyId}", lobbyId);
                return (false, "Error placing bid. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> ConfirmSaleAsync(string lobbyId, int playerId)
        {
            try
            {
                var auctionState = await _auctionStateRepo.GetAuctionStateAsync(lobbyId);
                if (auctionState?.CurrentPlayerId != playerId || auctionState.CurrentHighestBidderTeamId == null)
                    return (false, "No valid bid to confirm");

                var team = await _teamRepo.GetTeamAsync(auctionState.CurrentHighestBidderTeamId.Value);
                var player = await _playerRepo.GetPlayerAsync(playerId);
                if (team == null || player == null)
                    return (false, "Invalid data");

                // Double-check team can afford (in case points changed)
                if (team.RemainingPoints < auctionState.CurrentHighestBid.Value)
                    return (false, "Team no longer has sufficient points");

                // Update player as sold
                await _playerRepo.UpdatePlayerSoldAsync(playerId, team.TeamId, auctionState.CurrentHighestBid.Value);

                // Deduct points and increment player count atomically
                await _teamRepo.DeductTeamPointsAsync(team.TeamId, auctionState.CurrentHighestBid.Value);
                await _teamRepo.UpdateTeamPlayerCountAsync(team.TeamId, team.PlayerCount + 1);

                // Clear auction state
                await _auctionStateRepo.ClearCurrentAuctionAsync(lobbyId);

                // Notify after successful commit
                await _notificationService.NotifyPlayerSold(lobbyId, playerId, player.PlayerName, team.TeamId, team.TeamName, auctionState.CurrentHighestBid.Value, player.Position);
                await _notificationService.NotifyTeamUpdate(lobbyId, team.TeamId, team.TeamName, team.RemainingPoints - auctionState.CurrentHighestBid.Value);

                // Notify clients that no player is currently in auction (clears the auction panel)
                await _notificationService.NotifyPlayerUpdate(lobbyId, null, null, null);

                // Check if all players are sold
                var remainingPlayers = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
                if (remainingPlayers.Count == 0)
                {
                    var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
                    if (lobby != null) await _lobbyRepo.UpdateLobbyActiveStateAsync(lobbyId, false);
                    await _notificationService.NotifyAuctionComplete(lobbyId, "All players have been auctioned!");
                }

                // Notify viewers of updated available players
                var updatedRemaining = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
                await _notificationService.NotifyAvailablePlayersUpdate(lobbyId, updatedRemaining);

                _logger.LogInformation("Sale confirmed: {PlayerName} sold to {TeamName} for {Price} in lobby {LobbyId}",
                    player.PlayerName, team.TeamName, auctionState.CurrentHighestBid.Value, lobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming sale in lobby {LobbyId}", lobbyId);
                return (false, "Error confirming sale. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> SkipPlayerAsync(string lobbyId)
        {
            await _auctionStateRepo.ClearCurrentAuctionAsync(lobbyId);
            await _notificationService.NotifyPlayerUpdate(lobbyId, null, null, null);

            // Notify all clients of the updated available players list (the skipped player goes back to available)
            var updatedAvailable = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
            await _notificationService.NotifyAvailablePlayersUpdate(lobbyId, updatedAvailable);

            _logger.LogInformation("Player skipped in lobby {LobbyId}", lobbyId);
            return (true, null);
        }

        public async Task<(bool Success, bool IsPaused, string? Error)> TogglePauseAsync(string lobbyId)
        {
            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
            if (lobby == null)
                return (false, false, "Lobby not found");

            var newPausedState = !lobby.IsPaused;
            await _lobbyRepo.UpdateLobbyPauseStateAsync(lobbyId, newPausedState);
            await _notificationService.NotifyPauseUpdate(lobbyId, newPausedState);

            _logger.LogInformation("Auction {State} in lobby {LobbyId}", newPausedState ? "paused" : "resumed", lobbyId);
            return (true, newPausedState, null);
        }

        public async Task<(bool Success, string? Error)> AddPointsAsync(string lobbyId, int teamId, int additionalPoints)
        {
            if (additionalPoints <= 0 || additionalPoints > 100000)
                return (false, "Points must be between 1 and 100,000");

            await _teamRepo.AddPointsToTeamAsync(teamId, additionalPoints);
            var team = await _teamRepo.GetTeamAsync(teamId);
            await _notificationService.NotifyTeamUpdate(lobbyId, teamId, team?.TeamName ?? "", team?.RemainingPoints);

            _logger.LogInformation("Added {Points} points to team {TeamName} in lobby {LobbyId}", additionalPoints, team?.TeamName, lobbyId);
            return (true, null);
        }

        // ─── Enhanced Host Controls ───

        public async Task<(bool Success, string? Error)> ValidateViewerAccessAsync(string lobbyId)
        {
            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId.ToUpperInvariant());
            if (lobby == null)
                return (false, "Lobby not found");

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> RevokeSaleAsync(string lobbyId, int playerId)
        {
            try
            {
                var player = await _playerRepo.GetPlayerAsync(playerId);
                if (player == null || !player.IsAuctioned || player.SoldToTeamId == null || player.SoldPrice == null)
                    return (false, "Player has not been sold or not found");

                if (player.LobbyId != lobbyId)
                    return (false, "Player does not belong to this lobby");

                var teamId = player.SoldToTeamId.Value;
                var refundAmount = player.SoldPrice.Value;
                var team = await _teamRepo.GetTeamAsync(teamId);
                if (team == null)
                    return (false, "Team not found");

                // Return player to available
                await _playerRepo.RevokePlayerSaleAsync(playerId);

                // Refund points to team
                await _teamRepo.AddPointsToTeamAsync(teamId, refundAmount);
                await _teamRepo.UpdateTeamPlayerCountAsync(teamId, team.PlayerCount - 1);

                // Delete all bids for this player
                await _bidRepo.DeleteBidsForPlayerAsync(playerId);

                // Reactivate lobby if it was ended
                var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
                if (lobby != null && !lobby.IsActive)
                {
                    await _lobbyRepo.UpdateLobbyActiveStateAsync(lobbyId, true);
                }

                // Notify all clients
                await _notificationService.NotifySaleRevoked(lobbyId, playerId, player.PlayerName, teamId, team.TeamName, refundAmount);
                await _notificationService.NotifyTeamUpdate(lobbyId, teamId, team.TeamName, team.RemainingPoints + refundAmount);

                // Notify viewers of updated available players
                var updatedRemaining = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
                await _notificationService.NotifyAvailablePlayersUpdate(lobbyId, updatedRemaining);

                _logger.LogInformation("Sale revoked: Player {PlayerName} returned from {TeamName}, refund {Refund} in lobby {LobbyId}",
                    player.PlayerName, team.TeamName, refundAmount, lobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking sale in lobby {LobbyId}", lobbyId);
                return (false, "Error revoking sale. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> ResetCurrentBidAsync(string lobbyId)
        {
            try
            {
                var auctionState = await _auctionStateRepo.GetAuctionStateAsync(lobbyId);
                if (auctionState?.CurrentPlayerId == null)
                    return (false, "No player is currently in auction");

                var player = await _playerRepo.GetPlayerAsync(auctionState.CurrentPlayerId.Value);
                if (player == null)
                    return (false, "Player not found");

                // Delete all bids for the current player
                await _bidRepo.DeleteBidsForPlayerAsync(auctionState.CurrentPlayerId.Value);

                // Reset auction state to fresh start for this player
                auctionState.CurrentHighestBid = null;
                auctionState.CurrentHighestBidderTeamId = null;
                auctionState.AuctionStartTime = DateTime.UtcNow;
                await _auctionStateRepo.UpdateAuctionStateAsync(auctionState);

                // Notify all clients
                await _notificationService.NotifyBidReset(lobbyId, player.PlayerId, player.PlayerName, player.Position);

                _logger.LogInformation("Bids reset for player {PlayerName} in lobby {LobbyId}", player.PlayerName, lobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting bids in lobby {LobbyId}", lobbyId);
                return (false, "Error resetting bids. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> EndAuctionAsync(string lobbyId)
        {
            try
            {
                var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
                if (lobby == null)
                    return (false, "Lobby not found");

                await _lobbyRepo.UpdateLobbyActiveStateAsync(lobbyId, false);

                // Clear current auction
                await _auctionStateRepo.ClearCurrentAuctionAsync(lobbyId);
                await _notificationService.NotifyPlayerUpdate(lobbyId, null, null, null);
                await _notificationService.NotifyAuctionComplete(lobbyId, "Auction has been ended by the host.");

                _logger.LogInformation("Auction ended manually for lobby {LobbyId}", lobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending auction in lobby {LobbyId}", lobbyId);
                return (false, "Error ending auction. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> ReactivateAuctionAsync(string lobbyId)
        {
            try
            {
                var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
                if (lobby == null)
                    return (false, "Lobby not found");

                if (lobby.IsActive)
                    return (false, "Auction is already active");

                await _lobbyRepo.UpdateLobbyActiveStateAsync(lobbyId, true);
                await _lobbyRepo.UpdateLobbyPauseStateAsync(lobbyId, false);
                await _notificationService.NotifyAuctionReactivated(lobbyId);

                _logger.LogInformation("Auction reactivated for lobby {LobbyId}", lobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reactivating auction in lobby {LobbyId}", lobbyId);
                return (false, "Error reactivating auction. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> SetTeamPointsAsync(int teamId, int points)
        {
            if (points < 0 || points > 1000000)
                return (false, "Points must be between 0 and 1,000,000");

            try
            {
                var team = await _teamRepo.GetTeamAsync(teamId);
                if (team == null)
                    return (false, "Team not found");

                await _teamRepo.UpdateTeamPointsAsync(teamId, points);
                await _notificationService.NotifyTeamUpdate(team.LobbyId, teamId, team.TeamName, points);

                _logger.LogInformation("Team {TeamName} points set to {Points} in lobby {LobbyId}", team.TeamName, points, team.LobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting team points for team {TeamId}", teamId);
                return (false, "Error setting team points. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> DeductTeamPointsAsync(string lobbyId, int teamId, int points)
        {
            if (points <= 0 || points > 1000000)
                return (false, "Points to deduct must be between 1 and 1,000,000");

            try
            {
                var team = await _teamRepo.GetTeamAsync(teamId);
                if (team == null)
                    return (false, "Team not found");

                if (team.RemainingPoints < points)
                    return (false, "Team doesn't have enough points to deduct");

                await _teamRepo.DeductTeamPointsAsync(teamId, points);
                var updatedTeam = await _teamRepo.GetTeamAsync(teamId);
                await _notificationService.NotifyTeamUpdate(lobbyId, teamId, team.TeamName, updatedTeam?.RemainingPoints);

                _logger.LogInformation("Deducted {Points} from team {TeamName} in lobby {LobbyId}", points, team.TeamName, lobbyId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deducting team points for team {TeamId}", teamId);
                return (false, "Error deducting team points. Please try again.");
            }
        }

        public async Task<(bool Success, int Duration, string? Error)> SetTimerDurationAsync(string lobbyId, int durationSeconds)
        {
            if (durationSeconds < 5 || durationSeconds > 300)
                return (false, 30, "Timer duration must be between 5 and 300 seconds");

            try
            {
                await _auctionStateRepo.SetTimerDurationAsync(lobbyId, durationSeconds);
                await _notificationService.NotifyTimerUpdate(lobbyId, durationSeconds);

                _logger.LogInformation("Timer duration set to {Duration}s for lobby {LobbyId}", durationSeconds, lobbyId);
                return (true, durationSeconds, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting timer duration in lobby {LobbyId}", lobbyId);
                return (false, 30, "Error setting timer duration. Please try again.");
            }
        }

        public async Task<(bool Success, string? Error)> SetBidIncrementAsync(string lobbyId, int bidIncrement)
        {
            if (bidIncrement < 0)
                return (false, "Bid increment must be 0 or positive");

            if (bidIncrement > 10000)
                return (false, "Bid increment cannot exceed 10,000");

            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
            if (lobby == null)
                return (false, "Lobby not found");

            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            await connection.ExecuteAsync(
                "UPDATE Lobbies SET BidIncrement = @BidIncrement WHERE LobbyId = @LobbyId",
                new { BidIncrement = bidIncrement, LobbyId = lobbyId });

            await _notificationService.NotifyBidIncrementUpdate(lobbyId, bidIncrement);

            _logger.LogInformation("Bid increment set to {BidIncrement} for lobby {LobbyId}", bidIncrement, lobbyId);
            return (true, null);
        }

        public async Task<(bool Success, bool IsSuspended, string? Error)> ToggleTeamSuspensionAsync(string lobbyId, int teamId)
        {
            var team = await _teamRepo.GetTeamAsync(teamId);
            if (team == null)
                return (false, false, "Team not found");

            if (team.LobbyId != lobbyId)
                return (false, false, "Team does not belong to this lobby");

            var newSuspensionState = !team.IsSuspended;
            await _teamRepo.UpdateTeamSuspensionAsync(teamId, newSuspensionState);

            await _notificationService.NotifyTeamSuspension(lobbyId, teamId, team.TeamName, newSuspensionState);

            _logger.LogInformation("Team {TeamName} (ID:{TeamId}) suspended={IsSuspended} in lobby {LobbyId}",
                team.TeamName, teamId, newSuspensionState, lobbyId);
            return (true, newSuspensionState, null);
        }

        // ─── Player Management ───
        public async Task<int> AddPlayerAsync(string lobbyId, string playerName, string position)
        {
            var players = await _playerRepo.GetPlayersByLobbyAsync(lobbyId);
            var maxOrder = players.Any() ? players.Max(p => p.DisplayOrder) : 0;

            var player = new Player
            {
                LobbyId = lobbyId,
                PlayerName = playerName.Trim(),
                Position = position.Trim(),
                IsAuctioned = false,
                DisplayOrder = maxOrder + 1
            };

            var id = await _playerRepo.CreatePlayerAsync(player);
            _logger.LogInformation("Player {PlayerName} added to lobby {LobbyId}", playerName, lobbyId);
            
            // Notify viewers of updated available players
            var updatedRemaining = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
            await _notificationService.NotifyAvailablePlayersUpdate(lobbyId, updatedRemaining);
            
            return id;
        }

        public async Task<int> ImportPlayersAsync(string lobbyId, string playersData)
        {
            if (string.IsNullOrWhiteSpace(playersData)) return 0;

            var lines = playersData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var players = await _playerRepo.GetPlayersByLobbyAsync(lobbyId);
            var maxOrder = players.Any() ? players.Max(p => p.DisplayOrder) : 0;
            var count = 0;

            foreach (var line in lines)
            {
                var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    var player = new Player
                    {
                        LobbyId = lobbyId,
                        PlayerName = parts[0].Trim(),
                        Position = parts[1].Trim(),
                        IsAuctioned = false,
                        DisplayOrder = ++maxOrder
                    };
                    await _playerRepo.CreatePlayerAsync(player);
                    count++;
                }
            }

            _logger.LogInformation("Imported {Count} players to lobby {LobbyId}", count, lobbyId);

            // Notify viewers of updated available players after import
            if (count > 0)
            {
                var updatedRemaining = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
                await _notificationService.NotifyAvailablePlayersUpdate(lobbyId, updatedRemaining);
            }

            return count;
        }

        public async Task DeletePlayerAsync(int playerId)
        {
            try
            {
                var player = await _playerRepo.GetPlayerAsync(playerId);
                if (player == null)
                {
                    _logger.LogWarning("Player {PlayerId} not found", playerId);
                    return;
                }

                if (player.IsAuctioned)
                {
                    _logger.LogWarning("Cannot delete player {PlayerId} - already auctioned", playerId);
                    return;
                }

                var lobbyId = player.LobbyId;
                
                await _playerRepo.DeletePlayerAsync(playerId);
                _logger.LogInformation("Player {PlayerId} deleted from lobby {LobbyId}", playerId, lobbyId);

                // Notify viewers of updated available players
                var updatedRemaining = await _playerRepo.GetUnsoldPlayersAsync(lobbyId);
                _logger.LogInformation("🟢 About to notify availability update for lobby {LobbyId} with {Count} players", lobbyId, updatedRemaining.Count);
                await _notificationService.NotifyAvailablePlayersUpdate(lobbyId, updatedRemaining);
                _logger.LogInformation("🟢 Notification sent for lobby {LobbyId}", lobbyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting player {PlayerId}", playerId);
            }
        }

        // ─── Dashboard Data ───
        public async Task<AuctionViewModel> GetHostDashboardDataAsync(string lobbyId)
        {
            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
            var teams = await _teamRepo.GetTeamsByLobbyAsync(lobbyId);
            var players = await _playerRepo.GetPlayersByLobbyAsync(lobbyId);
            var auctionState = await _auctionStateRepo.GetAuctionStateAsync(lobbyId);

            Player? currentPlayer = null;
            Team? currentBidder = null;
            List<Bid> currentBids = new();

            if (auctionState?.CurrentPlayerId != null)
            {
                currentPlayer = await _playerRepo.GetPlayerAsync(auctionState.CurrentPlayerId.Value);
                if (auctionState.CurrentHighestBidderTeamId != null)
                    currentBidder = await _teamRepo.GetTeamAsync(auctionState.CurrentHighestBidderTeamId.Value);
                currentBids = await _bidRepo.GetBidsForPlayerAsync(auctionState.CurrentPlayerId.Value);
            }

            return new AuctionViewModel
            {
                Lobby = lobby ?? new(),
                Teams = teams,
                CurrentPlayer = currentPlayer,
                CurrentHighestBid = auctionState?.CurrentHighestBid,
                CurrentHighestBidder = currentBidder,
                RemainingPlayers = players.Where(p => !p.IsAuctioned && p.PlayerId != (currentPlayer?.PlayerId ?? -1)).ToList(),
                SoldPlayers = players.Where(p => p.IsAuctioned).ToList(),
                CurrentBids = currentBids,
                IsPaused = lobby?.IsPaused ?? false,
                IsActive = lobby?.IsActive ?? false
            };
        }

        public async Task<TeamDashboardViewModel> GetTeamDashboardDataAsync(string lobbyId, int teamId)
        {
            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
            var team = await _teamRepo.GetTeamAsync(teamId);
            var allTeams = await _teamRepo.GetTeamsByLobbyAsync(lobbyId);
            var myPlayers = await _playerRepo.GetPlayersByTeamAsync(teamId);
            var auctionState = await _auctionStateRepo.GetAuctionStateAsync(lobbyId);

            Player? currentPlayer = null;
            string? currentBidderName = null;
            List<Bid> recentBids = new();

            if (auctionState?.CurrentPlayerId != null)
            {
                currentPlayer = await _playerRepo.GetPlayerAsync(auctionState.CurrentPlayerId.Value);
                if (auctionState.CurrentHighestBidderTeamId != null)
                {
                    var bidderTeam = await _teamRepo.GetTeamAsync(auctionState.CurrentHighestBidderTeamId.Value);
                    currentBidderName = bidderTeam?.TeamName;
                }
                recentBids = await _bidRepo.GetBidsForPlayerAsync(auctionState.CurrentPlayerId.Value);
            }

            var canBid = currentPlayer != null
                && !lobby?.IsPaused == true
                && team?.RemainingPoints > (auctionState?.CurrentHighestBid ?? 0)
                && team?.PlayerCount < (lobby?.MaxPlayersPerTeam ?? 0)
                && auctionState?.CurrentHighestBidderTeamId != teamId;

            var allPlayers = await _playerRepo.GetPlayersByLobbyAsync(lobbyId);
            var availablePlayers = allPlayers
                .Where(p => !p.IsAuctioned && p.PlayerId != (currentPlayer?.PlayerId ?? -1))
                .ToList();

            return new TeamDashboardViewModel
            {
                Team = team ?? new(),
                AllTeams = allTeams,
                MyPlayers = myPlayers,
                CurrentPlayer = currentPlayer,
                CurrentHighestBid = auctionState?.CurrentHighestBid,
                CurrentHighestBidderName = currentBidderName,
                RemainingPoints = team?.RemainingPoints ?? 0,
                CanBid = canBid,
                IsPaused = lobby?.IsPaused ?? false,
                MaxPlayersPerTeam = lobby?.MaxPlayersPerTeam ?? 0,
                CurrentPlayerCount = team?.PlayerCount ?? 0,
                RecentBids = recentBids,
                AvailablePlayers = availablePlayers
            };
        }

        public async Task<List<Bid>> GetBidHistoryAsync(string lobbyId, int playerId)
        {
            return await _bidRepo.GetBidsForPlayerAsync(playerId);
        }

        public async Task<AuctionSummaryViewModel> GetAuctionSummaryAsync(string lobbyId)
        {
            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
            var teams = await _teamRepo.GetTeamsByLobbyAsync(lobbyId);
            var allPlayers = await _playerRepo.GetPlayersByLobbyAsync(lobbyId);
            var soldPlayers = allPlayers.Where(p => p.IsAuctioned).ToList();
            var unsoldPlayers = allPlayers.Where(p => !p.IsAuctioned).ToList();
            var allBids = await _bidRepo.GetBidsForLobbyAsync(lobbyId);

            var totalSpent = soldPlayers.Sum(p => p.SoldPrice ?? 0);
            var totalRemaining = teams.Sum(t => t.RemainingPoints);
            var avgPrice = soldPlayers.Count > 0 ? (double)totalSpent / soldPlayers.Count : 0;
            var highestSold = soldPlayers.OrderByDescending(p => p.SoldPrice ?? 0).FirstOrDefault();

            // Build team summaries
            var teamSummaries = new List<TeamSummary>();
            foreach (var team in teams)
            {
                var teamPlayers = soldPlayers.Where(p => p.SoldToTeamId == team.TeamId).ToList();
                var teamSpent = teamPlayers.Sum(p => p.SoldPrice ?? 0);
                var initialPoints = lobby?.PointsPerTeam ?? 0;

                teamSummaries.Add(new TeamSummary
                {
                    TeamId = team.TeamId,
                    TeamName = team.TeamName,
                    OwnerName = team.OwnerName,
                    CaptainName = team.CaptainName,
                    InitialPoints = initialPoints,
                    RemainingPoints = team.RemainingPoints,
                    TotalSpent = teamSpent,
                    PlayerCount = team.PlayerCount,
                    SpendPercentage = initialPoints > 0 ? Math.Round((double)teamSpent / initialPoints * 100, 1) : 0,
                    AvgPricePerPlayer = teamPlayers.Count > 0 ? Math.Round((double)teamSpent / teamPlayers.Count, 1) : 0,
                    MaxPlayersAllowed = lobby?.MaxPlayersPerTeam ?? 0,
                    Players = teamPlayers.Select(p => new PlayerSummaryRow
                    {
                        PlayerId = p.PlayerId,
                        PlayerName = p.PlayerName,
                        Position = p.Position,
                        SoldPrice = p.SoldPrice,
                        SoldToTeam = team.TeamName,
                        SoldToTeamId = team.TeamId,
                        IsSold = true
                    }).OrderByDescending(p => p.SoldPrice).ToList()
                });
            }

            // Most expensive players (top 10)
            var mostExpensive = soldPlayers
                .OrderByDescending(p => p.SoldPrice ?? 0)
                .Take(10)
                .Select(p => new MostExpensivePlayer
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.PlayerName,
                    Position = p.Position,
                    SoldPrice = p.SoldPrice ?? 0,
                    SoldToTeam = teams.FirstOrDefault(t => t.TeamId == p.SoldToTeamId)?.TeamName ?? "Unknown",
                    SoldToTeamId = p.SoldToTeamId ?? 0
                }).ToList();

            // Position breakdown
            var positionGroups = allPlayers.GroupBy(p => p.Position);
            var positionBreakdowns = positionGroups.Select(g =>
            {
                var sold = g.Where(p => p.IsAuctioned).ToList();
                var spent = sold.Sum(p => p.SoldPrice ?? 0);
                return new PositionBreakdown
                {
                    Position = g.Key,
                    TotalCount = g.Count(),
                    SoldCount = sold.Count,
                    UnsoldCount = g.Count() - sold.Count,
                    TotalSpent = spent,
                    AveragePrice = sold.Count > 0 ? Math.Round((double)spent / sold.Count, 1) : 0,
                    HighestPrice = sold.Count > 0 ? sold.Max(p => p.SoldPrice ?? 0) : 0
                };
            }).OrderByDescending(pb => pb.TotalSpent).ToList();

            // Key highlights
            var highestBidder = teamSummaries.OrderByDescending(t => t.TotalSpent).FirstOrDefault();
            var mostEconomical = teamSummaries.Where(t => t.PlayerCount > 0).OrderBy(t => t.AvgPricePerPlayer).FirstOrDefault();
            var mostPlayers = teamSummaries.OrderByDescending(t => t.PlayerCount).FirstOrDefault();

            return new AuctionSummaryViewModel
            {
                Lobby = lobby ?? new(),
                TeamSummaries = teamSummaries,
                SoldPlayers = soldPlayers.Select(p => new PlayerSummaryRow
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.PlayerName,
                    Position = p.Position,
                    SoldPrice = p.SoldPrice,
                    SoldToTeam = teams.FirstOrDefault(t => t.TeamId == p.SoldToTeamId)?.TeamName,
                    SoldToTeamId = p.SoldToTeamId,
                    IsSold = true
                }).OrderByDescending(p => p.SoldPrice).ToList(),
                UnsoldPlayers = unsoldPlayers.Select(p => new PlayerSummaryRow
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.PlayerName,
                    Position = p.Position,
                    IsSold = false
                }).OrderBy(p => p.PlayerName).ToList(),
                MostExpensivePlayers = mostExpensive,
                PositionBreakdowns = positionBreakdowns,
                TotalPlayers = allPlayers.Count,
                SoldCount = soldPlayers.Count,
                UnsoldCount = unsoldPlayers.Count,
                TotalPointsSpent = totalSpent,
                TotalPointsRemaining = totalRemaining,
                AverageSoldPrice = Math.Round(avgPrice, 1),
                HighestSoldPrice = highestSold?.SoldPrice ?? 0,
                HighestSoldPlayer = highestSold?.PlayerName ?? "N/A",
                HighestSoldTeam = highestSold != null ? teams.FirstOrDefault(t => t.TeamId == highestSold.SoldToTeamId)?.TeamName ?? "N/A" : "N/A",
                HighestBidderTeam = highestBidder,
                MostEconomicalTeam = mostEconomical,
                MostPlayersTeam = mostPlayers
            };
        }

        public async Task<ViewerDashboardViewModel> GetViewerDashboardDataAsync(string lobbyId)
        {
            var lobby = await _lobbyRepo.GetLobbyAsync(lobbyId);
            var teams = await _teamRepo.GetTeamsByLobbyAsync(lobbyId);
            var players = await _playerRepo.GetPlayersByLobbyAsync(lobbyId);
            var auctionState = await _auctionStateRepo.GetAuctionStateAsync(lobbyId);

            Player? currentPlayer = null;
            Team? currentBidder = null;
            List<Bid> currentBids = new();
            List<Bid> recentBids = new();

            if (auctionState?.CurrentPlayerId != null)
            {
                currentPlayer = await _playerRepo.GetPlayerAsync(auctionState.CurrentPlayerId.Value);
                if (auctionState.CurrentHighestBidderTeamId != null)
                    currentBidder = await _teamRepo.GetTeamAsync(auctionState.CurrentHighestBidderTeamId.Value);
                currentBids = await _bidRepo.GetBidsForPlayerAsync(auctionState.CurrentPlayerId.Value);
            }

            recentBids = await _bidRepo.GetRecentBidsForLobbyAsync(lobbyId, 20);

            var soldPlayers = players.Where(p => p.IsAuctioned).ToList();
            var totalSpent = soldPlayers.Sum(p => p.SoldPrice ?? 0);

            return new ViewerDashboardViewModel
            {
                Lobby = lobby ?? new(),
                Teams = teams,
                CurrentPlayer = currentPlayer,
                CurrentHighestBid = auctionState?.CurrentHighestBid,
                CurrentHighestBidder = currentBidder,
                RemainingPlayers = players.Where(p => !p.IsAuctioned && p.PlayerId != (currentPlayer?.PlayerId ?? -1)).ToList(),
                SoldPlayers = soldPlayers,
                RecentBids = recentBids,
                CurrentBids = currentBids,
                IsPaused = lobby?.IsPaused ?? false,
                IsActive = lobby?.IsActive ?? false,
                TimerDuration = auctionState?.TimerDuration ?? 30,
                TotalSpent = totalSpent
            };
        }
    }
}