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

        // ─── Resume Lobby ───
        public IActionResult ResumeLobby()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResumeLobby(ResumeLobbyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.LobbyId = model.LobbyId.ToUpperInvariant().Trim();

            var (lobbyId, error) = await _auctionService.ValidateResumeLobbyAsync(model);
            if (error != null)
            {
                ModelState.AddModelError("", error);
                return View(model);
            }

            // Store host auth info in session
            HttpContext.Session.SetString("LobbyId", lobbyId);
            HttpContext.Session.SetString("Role", "Host");

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
            TempData["Message"] = "Player deleted successfully";
            return RedirectToAction("ManagePlayers", new { lobbyId });
        }

        // ─── Watch Auction (Viewer Entry) ───
        [HttpGet]
        public IActionResult WatchAuction()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WatchAuction(WatchAuctionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.LobbyId = model.LobbyId.ToUpperInvariant().Trim();
            var (success, error) = await _auctionService.ValidateViewerAccessAsync(model.LobbyId);
            if (!success)
            {
                ModelState.AddModelError("", error ?? "Lobby not found");
                return View(model);
            }

            return RedirectToAction("ViewerDashboard", new { lobbyId = model.LobbyId });
        }

        // ─── Viewer Dashboard ───
        [HttpGet("Auction/ViewerDashboard/{lobbyId}")]
        public async Task<IActionResult> ViewerDashboard(string lobbyId)
        {
            var (success, error) = await _auctionService.ValidateViewerAccessAsync(lobbyId);
            if (!success)
                return NotFound(error);

            var viewModel = await _auctionService.GetViewerDashboardDataAsync(lobbyId);
            return View(viewModel);
        }

        // ─── Auction Summary ───
        [HttpGet("Auction/AuctionSummary/{lobbyId}")]
        public async Task<IActionResult> AuctionSummary(string lobbyId)
        {
            var viewModel = await _auctionService.GetAuctionSummaryAsync(lobbyId);
            if (viewModel.Lobby.LobbyId == null || string.IsNullOrEmpty(viewModel.Lobby.LobbyId))
                return NotFound();

            return View(viewModel);
        }

        // ─── Export CSV ───
        [HttpGet("Auction/ExportSummaryCsv/{lobbyId}")]
        public async Task<IActionResult> ExportSummaryCsv(string lobbyId, string type = "all")
        {
            var summary = await _auctionService.GetAuctionSummaryAsync(lobbyId);
            if (summary.Lobby.LobbyId == null || string.IsNullOrEmpty(summary.Lobby.LobbyId))
                return NotFound();

            var csv = new System.Text.StringBuilder();
            var gameName = summary.Lobby.GameName ?? "Auction";
            var fileName = $"{gameName.Replace(" ", "_")}_Summary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            if (type == "teamwise")
            {
                csv.AppendLine("Team Name,Owner,Initial Points,Spent,Remaining,Players,Avg Price/Player,Spend %");
                foreach (var team in summary.TeamSummaries)
                {
                    csv.AppendLine($"\"{team.TeamName}\",\"{team.OwnerName}\",{team.InitialPoints},{team.TotalSpent},{team.RemainingPoints},{team.PlayerCount},{team.AvgPricePerPlayer},{team.SpendPercentage}%");
                }
                csv.AppendLine();
                csv.AppendLine("Team Name,Player Name,Position,Sold Price");
                foreach (var team in summary.TeamSummaries)
                {
                    foreach (var player in team.Players)
                    {
                        csv.AppendLine($"\"{team.TeamName}\",\"{player.PlayerName}\",\"{player.Position}\",{player.SoldPrice}");
                    }
                }
            }
            else if (type == "playerwise")
            {
                csv.AppendLine("Player Name,Position,Status,Sold Price,Bought By");
                foreach (var player in summary.SoldPlayers)
                {
                    csv.AppendLine($"\"{player.PlayerName}\",\"{player.Position}\",SOLD,{player.SoldPrice},\"{player.SoldToTeam}\"");
                }
                foreach (var player in summary.UnsoldPlayers)
                {
                    csv.AppendLine($"\"{player.PlayerName}\",\"{player.Position}\",UNSOLD,,");
                }
            }
            else // "all"
            {
                // Overview section
                csv.AppendLine("=== AUCTION SUMMARY ===");
                csv.AppendLine($"Game,{gameName}");
                csv.AppendLine($"Lobby Code,{summary.Lobby.LobbyId}");
                csv.AppendLine($"Host,{summary.Lobby.HostName}");
                csv.AppendLine($"Date,{summary.Lobby.CreatedAt:yyyy-MM-dd HH:mm}");
                csv.AppendLine();
                csv.AppendLine("=== KEY STATS ===");
                csv.AppendLine($"Total Players,{summary.TotalPlayers}");
                csv.AppendLine($"Sold,{summary.SoldCount}");
                csv.AppendLine($"Unsold,{summary.UnsoldCount}");
                csv.AppendLine($"Total Points Spent,{summary.TotalPointsSpent}");
                csv.AppendLine($"Total Points Remaining,{summary.TotalPointsRemaining}");
                csv.AppendLine($"Average Sold Price,{summary.AverageSoldPrice}");
                csv.AppendLine($"Most Expensive,{summary.HighestSoldPlayer} ({summary.HighestSoldPrice} pts - {summary.HighestSoldTeam})");
                if (summary.HighestBidderTeam != null)
                    csv.AppendLine($"Highest Bidder Team,{summary.HighestBidderTeam.TeamName} ({summary.HighestBidderTeam.TotalSpent} pts spent)");
                if (summary.MostPlayersTeam != null)
                    csv.AppendLine($"Most Players Team,{summary.MostPlayersTeam.TeamName} ({summary.MostPlayersTeam.PlayerCount} players)");
                csv.AppendLine();

                // Team Summary
                csv.AppendLine("=== TEAM SUMMARY ===");
                csv.AppendLine("Team Name,Owner,Initial Points,Spent,Remaining,Players,Avg Price/Player,Spend %");
                foreach (var team in summary.TeamSummaries)
                {
                    csv.AppendLine($"\"{team.TeamName}\",\"{team.OwnerName}\",{team.InitialPoints},{team.TotalSpent},{team.RemainingPoints},{team.PlayerCount},{team.AvgPricePerPlayer},{team.SpendPercentage}%");
                }
                csv.AppendLine();

                // Team-wise Player Breakdown
                csv.AppendLine("=== TEAM-WISE PLAYERS ===");
                csv.AppendLine("Team,Player Name,Position,Sold Price");
                foreach (var team in summary.TeamSummaries)
                {
                    foreach (var player in team.Players)
                    {
                        csv.AppendLine($"\"{team.TeamName}\",\"{player.PlayerName}\",\"{player.Position}\",{player.SoldPrice}");
                    }
                }
                csv.AppendLine();

                // All Players
                csv.AppendLine("=== ALL PLAYERS ===");
                csv.AppendLine("Player Name,Position,Status,Sold Price,Bought By");
                foreach (var player in summary.SoldPlayers)
                {
                    csv.AppendLine($"\"{player.PlayerName}\",\"{player.Position}\",SOLD,{player.SoldPrice},\"{player.SoldToTeam}\"");
                }
                foreach (var player in summary.UnsoldPlayers)
                {
                    csv.AppendLine($"\"{player.PlayerName}\",\"{player.Position}\",UNSOLD,,");
                }
                csv.AppendLine();

                // Position Breakdown
                csv.AppendLine("=== POSITION BREAKDOWN ===");
                csv.AppendLine("Position,Total,Sold,Unsold,Total Spent,Avg Price,Highest Price");
                foreach (var pos in summary.PositionBreakdowns)
                {
                    csv.AppendLine($"\"{pos.Position}\",{pos.TotalCount},{pos.SoldCount},{pos.UnsoldCount},{pos.TotalSpent},{pos.AveragePrice},{pos.HighestPrice}");
                }
                csv.AppendLine();

                // Top 10 Most Expensive
                csv.AppendLine("=== TOP 10 MOST EXPENSIVE ===");
                csv.AppendLine("Rank,Player Name,Position,Sold Price,Bought By");
                var rank = 1;
                foreach (var p in summary.MostExpensivePlayers)
                {
                    csv.AppendLine($"{rank},\"{p.PlayerName}\",\"{p.Position}\",{p.SoldPrice},\"{p.SoldToTeam}\"");
                    rank++;
                }
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            // Add BOM for Excel to recognize UTF-8
            var bom = System.Text.Encoding.UTF8.GetPreamble();
            var fullBytes = bom.Concat(bytes).ToArray();

            return File(fullBytes, "text/csv", fileName);
        }

        // ─── API Endpoints (AJAX) ───

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartPlayerAuction(string lobbyId, int playerId, int minimumBid = 0)
        {
            var (success, error) = await _auctionService.StartPlayerAuctionAsync(lobbyId, playerId, minimumBid);
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

        // ─── Enhanced Host Control API Endpoints ───

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSale(string lobbyId, int playerId)
        {
            var (success, error) = await _auctionService.RevokeSaleAsync(lobbyId, playerId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to revoke sale"));

            return Json(ApiResponse.Ok("Sale revoked successfully"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetCurrentBid(string lobbyId)
        {
            var (success, error) = await _auctionService.ResetCurrentBidAsync(lobbyId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to reset bids"));

            return Json(ApiResponse.Ok("Bids reset successfully"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndAuction(string lobbyId)
        {
            var (success, error) = await _auctionService.EndAuctionAsync(lobbyId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to end auction"));

            return Json(ApiResponse.Ok("Auction ended"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateAuction(string lobbyId)
        {
            var (success, error) = await _auctionService.ReactivateAuctionAsync(lobbyId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to reactivate auction"));

            return Json(ApiResponse.Ok("Auction reactivated"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetTeamPoints(int teamId, int points)
        {
            var (success, error) = await _auctionService.SetTeamPointsAsync(teamId, points);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to set team points"));

            return Json(ApiResponse.Ok("Team points updated"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeductTeamPoints(string lobbyId, int teamId, int points)
        {
            var (success, error) = await _auctionService.DeductTeamPointsAsync(lobbyId, teamId, points);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to deduct team points"));

            return Json(ApiResponse.Ok("Points deducted"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetTimerDuration(string lobbyId, int durationSeconds)
        {
            var (success, duration, error) = await _auctionService.SetTimerDurationAsync(lobbyId, durationSeconds);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to set timer duration"));

            return Json(ApiResponse.Ok(new { Duration = duration }, $"Timer set to {duration} seconds"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetBidIncrement(string lobbyId, int bidIncrement)
        {
            var (success, error) = await _auctionService.SetBidIncrementAsync(lobbyId, bidIncrement);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to set bid increment"));

            return Json(ApiResponse.Ok(new { BidIncrement = bidIncrement }, $"Bid increment set to {bidIncrement}"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTeamSuspension(string lobbyId, int teamId)
        {
            var (success, isSuspended, error) = await _auctionService.ToggleTeamSuspensionAsync(lobbyId, teamId);
            if (!success)
                return Json(ApiResponse.Fail(error ?? "Failed to toggle team suspension"));

            return Json(ApiResponse.Ok(new { IsSuspended = isSuspended }, isSuspended ? "Team suspended" : "Team unsuspended"));
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
                minimumBid = viewModel.MinimumBid,
                isPaused = viewModel.IsPaused,
                teams = viewModel.Teams.Select(t => new
                {
                    t.TeamId,
                    t.TeamName,
                    t.RemainingPoints,
                    t.PlayerCount
                }),
                availablePlayers = viewModel.RemainingPlayers.Select(p => new
                {
                    playerId = p.PlayerId,
                    playerName = p.PlayerName,
                    position = p.Position
                })
            });
        }

        // ─── Viewer Dashboard Data API ───
        [HttpGet]
        public async Task<IActionResult> ViewerDashboardData(string lobbyId)
        {
            var viewModel = await _auctionService.GetViewerDashboardDataAsync(lobbyId);
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
                minimumBid = viewModel.MinimumBid,
                isPaused = viewModel.IsPaused,
                isActive = viewModel.IsActive,
                timerDuration = viewModel.TimerDuration,
                teams = viewModel.Teams.Select(t => new
                {
                    t.TeamId,
                    t.TeamName,
                    t.RemainingPoints,
                    t.PlayerCount
                }),
                availableCount = viewModel.RemainingPlayers.Count,
                soldCount = viewModel.SoldPlayers.Count,
                totalSpent = viewModel.TotalSpent
            });
        }
    }
}
