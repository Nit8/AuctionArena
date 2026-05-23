using AuctionArena.Interfaces;
using AuctionArena.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionArena.Controllers
{
    public class AuctionController : Controller
    {
        private readonly IAuctionService _auctionService;
        private readonly IPlayerRepository _playerRepo;
        private readonly ILogger<AuctionController> _logger;

        public AuctionController(
            IAuctionService auctionService,
            IPlayerRepository playerRepo,
            ILogger<AuctionController> logger)
        {
            _auctionService = auctionService;
            _playerRepo = playerRepo;
            _logger = logger;
        }

        // ─── Home Page ───
        public IActionResult Index()
        {
            return View();
        }

        // ─── Create Lobby ───
        public IActionResult CreateLobby()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLobby(CreateLobbyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (lobbyId, error) = await _auctionService.CreateLobbyAsync(model);
            if (error != null)
            {
                ModelState.AddModelError("", error);
                return View(model);
            }

            return RedirectToAction("HostDashboard", new { lobbyId });
        }

        // ─── Join Lobby ───
        public IActionResult JoinLobby()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinLobby(JoinLobbyViewModel model)
        {
            model.LobbyId = model.LobbyId.ToUpperInvariant().Trim();

            var (team, error) = await _auctionService.ValidateJoinLobbyAsync(model);
            if (error != null)
            {
                ModelState.AddModelError("", error);
                return View(model);
            }

            // Store auth info in session cookie
            HttpContext.Session.SetString("LobbyId", model.LobbyId);
            HttpContext.Session.SetString("TeamId", team!.TeamId.ToString());
            HttpContext.Session.SetString("OwnerName", team.OwnerName);
            HttpContext.Session.SetString("Role", "TeamOwner");

            return RedirectToAction("TeamDashboard", new { lobbyId = model.LobbyId, teamId = team.TeamId });
        }

        // ─── Host Dashboard ───
        [HttpGet("Auction/HostDashboard/{lobbyId}")]
        public async Task<IActionResult> HostDashboard(string lobbyId)
        {
            var viewModel = await _auctionService.GetHostDashboardDataAsync(lobbyId);
            if (viewModel.Lobby.LobbyId == null || string.IsNullOrEmpty(viewModel.Lobby.LobbyId))
                return NotFound();

            // Store host auth info
            HttpContext.Session.SetString("LobbyId", lobbyId);
            HttpContext.Session.SetString("Role", "Host");

            return View(viewModel);
        }

        // ─── Team Dashboard ───
        [HttpGet("Auction/TeamDashboard/{lobbyId}/{teamId}")]
        public async Task<IActionResult> TeamDashboard(string lobbyId, int teamId)
        {
            var viewModel = await _auctionService.GetTeamDashboardDataAsync(lobbyId, teamId);
            if (viewModel.Team.TeamId == 0)
                return NotFound();

            return View(viewModel);
        }

        // ─── Manage Players ───
        [HttpGet("Auction/ManagePlayers/{lobbyId}")]
        public async Task<IActionResult> ManagePlayers(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId))
                return BadRequest("Lobby ID is required");

            var players = await _playerRepo.GetPlayersByLobbyAsync(lobbyId);
            ViewBag.LobbyId = lobbyId;
            return View(players);
        }

        // ─── Add Player ───
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlayer(string lobbyId, string playerName, string position)
        {
            if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(position))
            {
                TempData["Error"] = "Player name and position are required";
                return RedirectToAction("ManagePlayers", new { lobbyId });
            }

            await _auctionService.AddPlayerAsync(lobbyId, playerName.Trim(), position.Trim());
            return RedirectToAction("ManagePlayers", new { lobbyId });
        }

        // ─── Import Players ───
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportPlayers(string lobbyId, string playersData)
        {
            var count = await _auctionService.ImportPlayersAsync(lobbyId, playersData);
            TempData["Message"] = $"Successfully imported {count} players";
            return RedirectToAction("ManagePlayers", new { lobbyId });
        }

        // ─── Delete Player ───
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlayer(string lobbyId, int playerId)
        {
            await _auctionService.DeletePlayerAsync(playerId);
            return RedirectToAction("ManagePlayers", new { lobbyId });
        }

        // ─── API Endpoints (AJAX) ───

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartPlayerAuction(string lobbyId, int playerId)
        {
            var (success, error) = await _auctionService.StartPlayerAuctionAsync(lobbyId, playerId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to start auction"));

            return Json(ApiResponse.Ok("Auction started"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceBid(string lobbyId, int playerId, int teamId, int bidAmount)
        {
            var (success, error) = await _auctionService.PlaceBidAsync(lobbyId, playerId, teamId, bidAmount);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Bid failed"));

            return Json(ApiResponse.Ok("Bid placed successfully"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSale(string lobbyId, int playerId)
        {
            var (success, error) = await _auctionService.ConfirmSaleAsync(lobbyId, playerId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to confirm sale"));

            return Json(ApiResponse.Ok("Sale confirmed"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SkipPlayer(string lobbyId)
        {
            var (success, error) = await _auctionService.SkipPlayerAsync(lobbyId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to skip player"));

            return Json(ApiResponse.Ok("Player skipped"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePause(string lobbyId)
        {
            var (success, isPaused, error) = await _auctionService.TogglePauseAsync(lobbyId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to toggle pause"));

            return Json(ApiResponse.Ok(new { IsPaused = isPaused }, isPaused ? "Auction paused" : "Auction resumed"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPoints(string lobbyId, int teamId, int additionalPoints)
        {
            var (success, error) = await _auctionService.AddPointsAsync(lobbyId, teamId, additionalPoints);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to add points"));

            return Json(ApiResponse.Ok("Points added successfully"));
        }

        // ─── Bid History API ───
        [HttpGet]
        public async Task<IActionResult> BidHistory(string lobbyId, int playerId)
        {
            var bids = await _auctionService.GetBidHistoryAsync(lobbyId, playerId);
            return Json(ApiResponse.Ok(bids));
        }

        // ─── Auction State API (for reconnection) ───
        [HttpGet]
        public async Task<IActionResult> AuctionState(string lobbyId)
        {
            var viewModel = await _auctionService.GetHostDashboardDataAsync(lobbyId);
            return Json(new
            {
                currentPlayer = viewModel.CurrentPlayer != null ? new
                {
                    viewModel.CurrentPlayer.PlayerId,
                    viewModel.CurrentPlayer.PlayerName,
                    viewModel.CurrentPlayer.Position
                } : null,
                currentHighestBid = viewModel.CurrentHighestBid,
                currentHighestBidder = viewModel.CurrentHighestBidder?.TeamName,
                isPaused = viewModel.IsPaused,
                teams = viewModel.Teams.Select(t => new
                {
                    t.TeamId,
                    t.TeamName,
                    t.RemainingPoints,
                    t.PlayerCount
                })
            });
        }
    }
}
