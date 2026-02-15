# 🏆 Live Auction App - Player Auction System

A real-time player auction web application for sports teams built with ASP.NET Core, C#, SQLite, and SignalR.

## 🌟 Features

### For Hosts
- ✅ Create auction lobbies with custom settings
- ✅ Password-protected lobbies
- ✅ Add/import players (manual and CSV format)
- ✅ Control auction flow (start, pause, resume, skip)
- ✅ Real-time monitoring of all bids
- ✅ Add extra points to teams during auction
- ✅ Manage team rosters and player limits

### For Team Owners
- ✅ Join lobbies with lobby code
- ✅ Real-time bidding interface
- ✅ Quick bid buttons for faster bidding
- ✅ View team roster and remaining points
- ✅ Lightweight, mobile-friendly interface
- ✅ Live updates when other owners bid

### Real-Time Features
- ✅ Instant bid updates across all devices
- ✅ Live player sold notifications
- ✅ Pause/resume synchronization
- ✅ Team points and roster updates

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK or later
- Any modern web browser
- All devices must be on the same WiFi network

### Installation

1. **Download/Clone the project**
   ```bash
   cd AuctionApp
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

4. **Access the application**
   - The console will display all available IP addresses
   - Example output:
   ```
   ==============================================
   AUCTION APP IS RUNNING
   ==============================================
   Access the application from any device on your network:
     http://192.168.1.100:5000
     http://127.0.0.1:5000
   ==============================================
   ```

5. **Share the IP with participants**
   - Give the IP address (e.g., `http://192.168.1.100:5000`) to all team owners
   - They can access it from any device on the same network

## 📖 How to Use

### Step 1: Host Creates Lobby

1. Go to the home page
2. Click "Create Lobby"
3. Fill in the details:
   - Your name (host name)
   - Game/Sport name (e.g., "Football", "Cricket")
   - Optional password for privacy
   - Number of teams (2-12)
   - Points per team (budget for bidding)
   - Players per team (target roster size)
   - Min/Max players per team
4. Set up each team:
   - Team name
   - Owner name (exact name owners will use to login)
   - Optional captain name
5. Click "Create Lobby"
6. **Save the Lobby Code** - share this with all team owners!

### Step 2: Add Players

1. After creating lobby, click "Manage Players"
2. **Option A - Add Single Player:**
   - Enter player name and position
   - Click "Add"
3. **Option B - Import Multiple Players:**
   - Use CSV format: `Name,Position`
   - Example:
   ```
   Cristiano Ronaldo,Forward
   Lionel Messi,Forward
   Kevin De Bruyne,Midfielder
   Virgil van Dijk,Defender
   ```
   - Click "Import Players"
4. Click "Go to Auction" when ready

### Step 3: Team Owners Join

1. Team owners go to the shared IP address
2. Click "Join Lobby"
3. Enter:
   - Lobby code (from host)
   - Their exact owner name (as registered by host)
   - Password (if set)
4. Click "Join Lobby"

### Step 4: Run the Auction

**Host Controls:**
1. Select a player from "Available Players" list
2. Click "Start" to begin auction
3. Monitor bids in real-time
4. Click "✅ Confirm Sale" when bidding is complete
5. Or click "⏭️ Skip Player" to auction later
6. Use "⏸️ Pause" to temporarily halt the auction
7. Click "💰 Add Points" to give a team extra points

**Team Owner Actions:**
1. Wait for host to start a player auction
2. View current player and highest bid
3. Enter bid amount or use quick bid buttons (+50, +100, etc.)
4. Click "Place Bid"
5. Keep bidding until you win or decide to stop
6. View your team roster at the bottom

### Step 5: Complete the Auction

- Continue until all players are sold
- Host can skip players and come back to them later
- Final rosters will be visible to all participants

## 🎮 Auction Rules

1. **Bidding:**
   - Must bid higher than current highest bid
   - Cannot exceed your remaining points
   - Cannot bid if team has reached max players

2. **Team Limits:**
   - Minimum players: Set by host (default 8)
   - Maximum players: Set by host (default 15)

3. **Points:**
   - Each team starts with same points
   - Host can add extra points during auction
   - Points deducted when player is won

4. **Player Status:**
   - Available: Not yet auctioned
   - In Auction: Currently being bid on
   - Sold: Assigned to a team

## 📱 Network Setup

### For Host Computer:

1. **Find your IP address:**
   - **Windows:** Open Command Prompt → type `ipconfig` → look for IPv4 Address
   - **Mac/Linux:** Open Terminal → type `ifconfig` or `ip addr`
   - Usually looks like: 192.168.1.x or 10.0.0.x

2. **Configure firewall** (if needed):
   - Allow port 5000 through your firewall
   - Windows: Settings → Firewall → Allow an app
   - Mac: System Preferences → Security → Firewall

3. **Start the application:**
   ```bash
   dotnet run
   ```

### For Team Owners:

1. Connect to the same WiFi network as the host
2. Open browser and go to: `http://[HOST_IP]:5000`
3. Replace [HOST_IP] with the actual host IP address

## 🎨 Interface Overview

### Host Dashboard
- **Current Auction Panel:** Shows player being auctioned and current bids
- **Teams Overview:** All teams with their points and player count
- **Available Players:** List of players to auction
- **Sold Players:** Players already assigned to teams
- **Controls:** Pause, add points, manage players

### Team Owner Dashboard  
- **Points Display:** Your remaining budget
- **Player Count:** Number of players in your team
- **Status:** Auction active/paused
- **Current Auction:** Player details and bid input
- **My Team:** Your roster of won players

## 🛠️ Technical Details

### Technology Stack
- **Backend:** ASP.NET Core 8.0, C#
- **Database:** SQLite3 (file-based, portable)
- **Real-time:** SignalR WebSockets
- **Frontend:** Bootstrap 5, Vanilla JavaScript
- **Architecture:** MVC Pattern

### Database Schema
- **Lobbies:** Auction session data
- **Teams:** Team information and points
- **Players:** Player details and status
- **Bids:** Bid history
- **AuctionState:** Current auction status

### Port Configuration
- Default: Port 5000
- Accessible on all network interfaces (0.0.0.0)
- To change port, edit `Program.cs`:
  ```csharp
  builder.WebHost.UseUrls("http://0.0.0.0:YOUR_PORT");
  ```

## 🐛 Troubleshooting

### "Cannot connect to lobby"
- Ensure all devices are on the same WiFi network
- Check firewall settings on host computer
- Verify you're using the correct IP address
- Try disabling VPN if enabled

### "Insufficient points" error
- You've run out of bidding budget
- Ask host to add points using "Add Points" button
- Or wait for other players and bid carefully

### "Lobby not found"
- Check the lobby code is correct (case-insensitive)
- Ensure the host application is still running
- Lobby code must be exactly 8 characters

### Real-time updates not working
- Refresh the page
- Check browser console for errors
- Ensure stable network connection
- Try a different browser

### Players not showing up
- Click "Manage Players" from host dashboard
- Ensure players were imported correctly
- Check CSV format: `Name,Position` (no headers)

## 📋 CSV Import Format

```csv
Cristiano Ronaldo,Forward
Lionel Messi,Forward
Neymar Jr,Forward
Kevin De Bruyne,Midfielder
Luka Modric,Midfielder
N'Golo Kante,Midfielder
Virgil van Dijk,Defender
Sergio Ramos,Defender
Thibaut Courtois,Goalkeeper
```

**Important:**
- No header row
- Format: `PlayerName,Position`
- Each player on new line
- Commas separate name and position

## 🎯 Best Practices

1. **Before Starting:**
   - Test the network connection
   - Add all players before auction starts
   - Brief all owners on the rules
   - Set appropriate min/max player limits

2. **During Auction:**
   - Give teams time to decide on bids
   - Use pause feature for breaks
   - Skip expensive players if needed
   - Monitor team budgets

3. **For Team Owners:**
   - Plan your strategy based on budget
   - Don't spend all points early
   - Keep track of remaining roster spots
   - Use quick bid buttons for speed

## 🔒 Security Notes

- Passwords are stored in plain text (lobby-level only)
- No user authentication system
- Designed for trusted local networks
- Not recommended for public internet use
- Use strong passwords for privacy

## 📄 License

This is an open-source project. Feel free to modify and use as needed.

## 🤝 Support

For issues or questions:
1. Check this README
2. Review troubleshooting section
3. Check console logs for errors
4. Ensure all prerequisites are met

## 🎉 Enjoy Your Auction!

Have fun building your dream team! 🏆⚽🏀🏏
