using AuctionArena.Hubs;
using AuctionArena.Interfaces;
using AuctionArena.Repositories;
using AuctionArena.Services;
using AuctionArena.Services.Notifications;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Session support for auth
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.Name = "AuctionArena.Session";
});

// Database service (initializes schema)
builder.Services.AddSingleton<DatabaseService>();

// Repositories
builder.Services.AddScoped<ILobbyRepository, LobbyRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IBidRepository, BidRepository>();
builder.Services.AddScoped<IAuctionStateRepository, AuctionStateRepository>();

// Services
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();

// Configure to listen on all network interfaces (port configurable via ASPNETCORE_URLS env var / web.config)
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:6869";
builder.WebHost.UseUrls(urls);
var port = new Uri(urls.Split(';')[0]).Port;

// Add structured logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

var dbService = app.Services.GetRequiredService<DatabaseService>();


// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auction}/{action=Index}/{id?}");

app.MapHub<AuctionHub>("/auctionHub");

// Display the IP addresses where the app is accessible
var addresses = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
    .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
    .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    .Select(addr => addr.Address.ToString());

Console.WriteLine("\n==============================================");
Console.WriteLine("AUCTION ARENA IS RUNNING");
Console.WriteLine("==============================================");
Console.WriteLine("Access the application from any device on your network:");
foreach (var addr in addresses)
{
    Console.WriteLine($"  http://{addr}:{port}");
}
Console.WriteLine("==============================================\n");

app.Run();
