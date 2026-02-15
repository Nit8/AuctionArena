using AuctionArena.Hubs;
using AuctionArena.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<DatabaseService>();

// Configure to listen on all network interfaces
builder.WebHost.UseUrls("http://0.0.0.0:6869");

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

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
    Console.WriteLine($"  http://{addr}:6869");
}
Console.WriteLine("==============================================\n");

app.Run();
