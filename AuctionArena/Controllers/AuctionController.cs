using AuctionArena.Services;
using Microsoft.AspNetCore.Mvc;
using AuctionArena.Models;

namespace AuctionArena.Controllers
{
    public class AuctionController : Controller
    {
        private readonly DatabaseService _db;
        public AuctionController(DatabaseService db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult About()
        {
            return View();
        }
        public IActionResult CreateLobby()
        {
            return View();
        }
        // POST: Handle form submission
        [HttpPost]
        public async Task<IActionResult> CreateLobby(string hostName,
                                                     string gameName,
                                                     int totalTeams,
                                                     int pointsPerTeam)
        {
            // Generate random lobby code
            var lobbyId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            // Create lobby object
            var lobby = new Lobby
            {
                LobbyId = lobbyId,
                HostName = hostName,
                GameName = gameName,
                TotalTeams = totalTeams,
                PointsPerTeam = pointsPerTeam,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Save to database
            await _db.CreateLobby(lobby);

            // Show success message
            ViewBag.LobbyCode = lobbyId;
            return View("LobbyCreated");
        }
    }
}
