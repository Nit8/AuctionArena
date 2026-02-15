using System.Diagnostics;
using AuctionArena.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuctionArena.Controllers
{
    public class HomeController : Controller
    {
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
            ViewBag.Message = "Welcome to Auction Arena! This is a platform where you can create and join lobbies to participate in exciting player auctions. Whether you're a team owner looking to build your dream team or a player eager to showcase your skills, Auction Arena has something for everyone. Join us and experience the thrill of the auction floor!";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
