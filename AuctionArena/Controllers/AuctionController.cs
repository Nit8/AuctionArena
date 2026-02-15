using AuctionArena.Hubs;
using AuctionArena.Models;
using AuctionArena.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AuctionArena.Controllers
{
    public class AuctionController : Controller
    {
        private readonly DatabaseService _db;
        private readonly IHubContext<AuctionHub> _hubContext;

        public AuctionController(DatabaseService db, IHubContext<AuctionHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        // Home page
        public IActionResult Index()
        {
            return View();
        }

        // Create Lobby - GET
        public IActionResult CreateLobby()
        {
            return View();
        }

        // Create Lobby - POST
        [HttpPost]
        public async Task<IActionResult> CreateLobby(CreateLobbyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var lobbyId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            var lobby = new Lobby
            {
                LobbyId = lobbyId,
                HostName = model.HostName,
                GameName = model.GameName,
                Password = model.Password,
                TotalTeams = model.TotalTeams,
                PlayersPerTeam = model.PlayersPerTeam,
                PointsPerTeam = model.PointsPerTeam,
                MinPlayersPerTeam = model.MinPlayersPerTeam,
                MaxPlayersPerTeam = model.MaxPlayersPerTeam,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPaused = false
            };

            await _db.CreateLobby(lobby);

            // Create teams
            foreach (var teamSetup in model.Teams)
            {
                var team = new Team
                {
                    LobbyId = lobbyId,
                    TeamName = teamSetup.TeamName,
                    OwnerName = teamSetup.OwnerName,
                    CaptainName = teamSetup.CaptainName,
                    RemainingPoints = model.PointsPerTeam,
                    PlayerCount = 0
                };
                await _db.CreateTeam(team);
            }

            return RedirectToAction("HostDashboard", new { lobbyId });
        }

        // Join Lobby - GET
        public IActionResult JoinLobby()
        {
            return View();
        }

        // Join Lobby - POST
        [HttpPost]
        public async Task<IActionResult> JoinLobby(JoinLobbyViewModel model)
        {
            var lobby = await _db.GetLobby(model.LobbyId);

            if (lobby == null)
            {
                ModelState.AddModelError("", "Lobby not found");
                return View(model);
            }

            if (!string.IsNullOrEmpty(lobby.Password) && lobby.Password != model.Password)
            {
                ModelState.AddModelError("", "Incorrect password");
                return View(model);
            }

            var team = await _db.GetTeamByOwnerName(model.LobbyId, model.OwnerName);

            if (team == null)
            {
                ModelState.AddModelError("", "You are not registered in this lobby");
                return View(model);
            }

            return RedirectToAction("TeamDashboard", new { lobbyId = model.LobbyId, teamId = team.TeamId });
        }

        // Host Dashboard
        [HttpGet("Auction/HostDashboard/{lobbyId}")]
        public async Task<IActionResult> HostDashboard(string lobbyId)
        {
            var lobby = await _db.GetLobby(lobbyId);
            if (lobby == null)
            {
                return NotFound();
            }

            var teams = await _db.GetTeamsByLobby(lobbyId);
            var players = await _db.GetPlayersByLobby(lobbyId);
            var auctionState = await _db.GetAuctionState(lobbyId);

            Player? currentPlayer = null;
            Team? currentBidder = null;

            if (auctionState?.CurrentPlayerId != null)
            {
                currentPlayer = await _db.GetPlayer(auctionState.CurrentPlayerId.Value);
                if (auctionState.CurrentHighestBidderTeamId != null)
                {
                    currentBidder = await _db.GetTeam(auctionState.CurrentHighestBidderTeamId.Value);
                }
            }

            var viewModel = new AuctionViewModel
            {
                Lobby = lobby,
                Teams = teams,
                CurrentPlayer = currentPlayer,
                CurrentHighestBid = auctionState?.CurrentHighestBid,
                CurrentHighestBidder = currentBidder,
                RemainingPlayers = players.Where(p => !p.IsAuctioned).ToList(),
                SoldPlayers = players.Where(p => p.IsAuctioned).ToList(),
                IsPaused = lobby.IsPaused
            };

            return View(viewModel);
        }

        // Team Dashboard
        [HttpGet("Auction/TeamDashboard/{lobbyId}/{teamId}")]
        public async Task<IActionResult> TeamDashboard(string lobbyId, int teamId)
        {
            var lobby = await _db.GetLobby(lobbyId);
            var team = await _db.GetTeam(teamId);

            if (lobby == null || team == null)
            {
                return NotFound();
            }

            var myPlayers = await _db.GetPlayersByTeam(teamId);
            var auctionState = await _db.GetAuctionState(lobbyId);

            Player? currentPlayer = null;
            string? currentBidderName = null;

            if (auctionState?.CurrentPlayerId != null)
            {
                currentPlayer = await _db.GetPlayer(auctionState.CurrentPlayerId.Value);
                if (auctionState.CurrentHighestBidderTeamId != null)
                {
                    var bidderTeam = await _db.GetTeam(auctionState.CurrentHighestBidderTeamId.Value);
                    currentBidderName = bidderTeam?.TeamName;
                }
            }

            var viewModel = new TeamDashboardViewModel
            {
                Team = team,
                MyPlayers = myPlayers,
                CurrentPlayer = currentPlayer,
                CurrentHighestBid = auctionState?.CurrentHighestBid,
                CurrentHighestBidderName = currentBidderName,
                RemainingPoints = team.RemainingPoints,
                CanBid = currentPlayer != null && team.RemainingPoints > (auctionState?.CurrentHighestBid ?? 0),
                IsPaused = lobby.IsPaused
            };

            return View(viewModel);
        }

        // Manage Players - GET
        [HttpGet("Auction/ManagePlayers/{lobbyId}")]
        public async Task<IActionResult> ManagePlayers(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                return BadRequest("Lobby ID is required");
            }

            var lobby = await _db.GetLobby(lobbyId);
            if (lobby == null)
            {
                return NotFound();
            }

            var players = await _db.GetPlayersByLobby(lobbyId);
            ViewBag.LobbyId = lobbyId;
            ViewBag.GameName = lobby.GameName;

            return View(players);
        }

        // Add Player - POST
        [HttpPost]
        public async Task<IActionResult> AddPlayer(string lobbyId, string playerName, string position)
        {
            var players = await _db.GetPlayersByLobby(lobbyId);
            var maxOrder = players.Any() ? players.Max(p => p.DisplayOrder) : 0;

            var player = new Player
            {
                LobbyId = lobbyId,
                PlayerName = playerName,
                Position = position,
                IsAuctioned = false,
                DisplayOrder = maxOrder + 1
            };

            await _db.CreatePlayer(player);
            return RedirectToAction("ManagePlayers", new { lobbyId });
        }

        // Import Players - POST
        [HttpPost]
        public async Task<IActionResult> ImportPlayers(string lobbyId, string playersData)
        {
            if (playersData is null) return RedirectToAction("ManagePlayers", new { lobbyId });
            var lines = playersData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var players = await _db.GetPlayersByLobby(lobbyId);
            var maxOrder = players.Any() ? players.Max(p => p.DisplayOrder) : 0;

            foreach (var line in lines)
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    var player = new Player
                    {
                        LobbyId = lobbyId,
                        PlayerName = parts[0],
                        Position = parts[1],
                        IsAuctioned = false,
                        DisplayOrder = ++maxOrder
                    };
                    await _db.CreatePlayer(player);
                }
            }

            return RedirectToAction("ManagePlayers", new { lobbyId });
        }

        // Start Auction for Player
        [HttpPost]
        public async Task<IActionResult> StartPlayerAuction(string lobbyId, int playerId)
        {
            var player = await _db.GetPlayer(playerId);
            if (player == null || player.IsAuctioned)
            {
                return BadRequest("Player not available");
            }

            var auctionState = new AuctionState
            {
                LobbyId = lobbyId,
                CurrentPlayerId = playerId,
                CurrentHighestBid = null,
                CurrentHighestBidderTeamId = null,
                AuctionStartTime = DateTime.UtcNow
            };

            await _db.UpdateAuctionState(auctionState);

            // Notify all clients
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceivePlayerUpdate", new
            {
                playerId = player.PlayerId,
                playerName = player.PlayerName,
                position = player.Position
            });

            return Ok();
        }

        // Place Bid
        [HttpPost]
        public async Task<IActionResult> PlaceBid(string lobbyId, int playerId, int teamId, int bidAmount)
        {
            var team = await _db.GetTeam(teamId);
            var auctionState = await _db.GetAuctionState(lobbyId);
            var lobby = await _db.GetLobby(lobbyId);

            if (team == null || lobby == null || lobby.IsPaused)
            {
                return BadRequest("Invalid bid");
            }

            if (auctionState?.CurrentPlayerId != playerId)
            {
                return BadRequest("This player is not currently in auction");
            }

            if (bidAmount > team.RemainingPoints)
            {
                return BadRequest("Insufficient points");
            }

            if (auctionState.CurrentHighestBid != null && bidAmount <= auctionState.CurrentHighestBid)
            {
                return BadRequest("Bid must be higher than current bid");
            }

            // Check team player count
            if (team.PlayerCount >= lobby.MaxPlayersPerTeam)
            {
                return BadRequest("Team has reached maximum players");
            }

            var bid = new Bid
            {
                LobbyId = lobbyId,
                PlayerId = playerId,
                TeamId = teamId,
                BidAmount = bidAmount,
                BidTime = DateTime.UtcNow
            };

            await _db.CreateBid(bid);

            auctionState.CurrentHighestBid = bidAmount;
            auctionState.CurrentHighestBidderTeamId = teamId;
            await _db.UpdateAuctionState(auctionState);

            // Notify all clients
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveBidUpdate", new
            {
                playerId,
                teamId,
                teamName = team.TeamName,
                bidAmount
            });

            return Ok();
        }

        // Confirm Sale
        [HttpPost]
        public async Task<IActionResult> ConfirmSale(string lobbyId, int playerId)
        {
            var auctionState = await _db.GetAuctionState(lobbyId);

            if (auctionState?.CurrentPlayerId != playerId || auctionState.CurrentHighestBidderTeamId == null)
            {
                return BadRequest("No valid bid to confirm");
            }

            var team = await _db.GetTeam(auctionState.CurrentHighestBidderTeamId.Value);
            var player = await _db.GetPlayer(playerId);

            if (team == null || player == null)
            {
                return BadRequest("Invalid data");
            }

            // Update player
            await _db.UpdatePlayerSold(playerId, team.TeamId, auctionState.CurrentHighestBid!.Value);

            // Update team
            await _db.UpdateTeamPoints(team.TeamId, team.RemainingPoints - auctionState.CurrentHighestBid.Value);
            await _db.UpdateTeamPlayerCount(team.TeamId, team.PlayerCount + 1);

            // Clear auction state
            await _db.ClearCurrentAuction(lobbyId);

            // Notify all clients
            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceivePlayerSold", new
            {
                playerId,
                playerName = player.PlayerName,
                teamId = team.TeamId,
                teamName = team.TeamName,
                soldPrice = auctionState.CurrentHighestBid.Value
            });

            return Ok();
        }

        // Skip Player
        [HttpPost]
        public async Task<IActionResult> SkipPlayer(string lobbyId)
        {
            await _db.ClearCurrentAuction(lobbyId);

            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceivePlayerUpdate", new
            {
                playerId = (int?)null,
                playerName = (string?)null,
                position = (string?)null
            });

            return Ok();
        }

        // Pause/Resume Auction
        [HttpPost]
        public async Task<IActionResult> TogglePause(string lobbyId)
        {
            var lobby = await _db.GetLobby(lobbyId);
            if (lobby == null)
            {
                return NotFound();
            }

            await _db.UpdateLobbyPauseState(lobbyId, !lobby.IsPaused);

            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceivePauseUpdate", !lobby.IsPaused);

            return Ok();
        }

        // Add Points to Team
        [HttpPost]
        public async Task<IActionResult> AddPoints(string lobbyId, int teamId, int additionalPoints)
        {
            await _db.AddPointsToTeam(teamId, additionalPoints);

            var team = await _db.GetTeam(teamId);

            await _hubContext.Clients.Group(lobbyId).SendAsync("ReceiveTeamUpdate", new
            {
                teamId,
                teamName = team?.TeamName,
                remainingPoints = team?.RemainingPoints
            });

            return Ok();
        }
    }
}
