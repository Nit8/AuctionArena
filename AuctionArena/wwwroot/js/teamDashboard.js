// AuctionArena - Team Dashboard Logic

let conn;
let timer;
let currentLobbyId;
let currentTeamId;
let currentPlayerId = 0;
let justSold = false;

async function initTeamDashboard(lobbyId, teamId, initialPlayerId = 0) {
    currentLobbyId = lobbyId;
    currentTeamId = teamId;
    currentPlayerId = initialPlayerId;

    conn = new AuctionConnection('/auctionHub');

    conn.on('bidUpdate', handleBidUpdate);
    conn.on('playerUpdate', handlePlayerUpdate);
    conn.on('playerSold', handlePlayerSold);
    conn.on('pauseUpdate', handlePauseUpdate);
    conn.on('teamUpdate', handleTeamUpdate);
    conn.on('auctionComplete', handleAuctionComplete);
    conn.on('reconnected', () => { fetchAuctionState(); });

    await conn.connect(lobbyId);

    // Fetch current auction state on first connect (in case we joined mid-auction)
    if (!initialPlayerId) {
        await fetchAuctionState();
    }

    timer = new CountdownTimer(null, 30);
}

function handleBidUpdate(data) {
    const display = document.getElementById('currentBidDisplay');
    if (display) {
        display.innerHTML = `
            <div class="bid-amount">${escapeHtml(data.bidAmount.toString())}</div>
            <div class="bid-team">by ${escapeHtml(data.teamName)}</div>
        `;
        display.classList.add('bid-animation');
        setTimeout(() => display.classList.remove('bid-animation'), 400);
    }

    // Update bid input minimum
    const bidInput = document.getElementById('bidAmount');
    if (bidInput) bidInput.min = data.bidAmount + 1;

    // Reset countdown
    timer.reset();

    // Enable/disable bid button based on whether it's our team
    const bidBtn = document.getElementById('bidButton');
    if (bidBtn && data.teamId !== currentTeamId) {
        bidBtn.disabled = false;
    } else if (bidBtn && data.teamId === currentTeamId) {
        bidBtn.disabled = true;
        ToastManager.show('You have the highest bid!', 'info', 2000);
    }

    // Update bid history
    addBidToHistory(data.teamName, data.bidAmount);
}

function handlePlayerUpdate(data) {
    const panel = document.getElementById('currentPlayerPanel');
    if (!panel) return;

    if (data.playerId) {
        currentPlayerId = data.playerId;
        justSold = false;
        panel.innerHTML = `
            <div class="text-center p-4">
                <h2 style="font-weight:800;margin-bottom:0.5rem">${escapeHtml(data.playerName)}</h2>
                <h5 style="color:var(--gray-500);margin-bottom:1.5rem">${escapeHtml(data.position)}</h5>
                <div class="bid-display" id="currentBidDisplay">
                    <h4 style="color:var(--gray-400);font-weight:600">No bids yet - Place the first bid!</h4>
                </div>
                <div id="countdownTimer" class="countdown-timer mt-3"></div>
                <div style="max-width:500px;margin:1.5rem auto 0">
                    <div style="display:flex;gap:0.5rem;margin-bottom:1rem">
                        <input type="number" class="form-control-auction form-control-lg-auction" id="bidAmount"
                               placeholder="Enter bid amount" min="1" style="flex:1">
                        <button class="btn-auction btn-success-auction btn-lg-auction" onclick="placeBid()" id="bidButton">
                            Bid
                        </button>
                    </div>
                    <div class="quick-bid-group">
                        <button class="quick-bid-btn" onclick="quickBid(50)">+50</button>
                        <button class="quick-bid-btn" onclick="quickBid(100)">+100</button>
                        <button class="quick-bid-btn" onclick="quickBid(200)">+200</button>
                        <button class="quick-bid-btn" onclick="quickBid(500)">+500</button>
                    </div>
                </div>
                <div id="bidError" class="mt-2" style="display:none"></div>
            </div>
        `;
        timer.start();
    } else {
        // If we just sold a player, keep the SOLD/YOU WON animation briefly
        if (justSold) {
            justSold = false;
            setTimeout(() => {
                currentPlayerId = 0;
                panel.innerHTML = `
            <div class="text-center p-5">
                <svg style="width:4rem;height:4rem;color:var(--gray-400);margin-bottom:1rem" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"/>
                </svg>
                <h4 style="color:var(--gray-400)">Waiting for next player...</h4>
                <p style="color:var(--gray-400)">The host will start the auction shortly</p>
            </div>
        `;
            }, 2500);
        } else {
            currentPlayerId = 0;
            panel.innerHTML = `
            <div class="text-center p-5">
                <svg style="width:4rem;height:4rem;color:var(--gray-400);margin-bottom:1rem" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"/>
                </svg>
                <h4 style="color:var(--gray-400)">Waiting for next player...</h4>
                <p style="color:var(--gray-400)">The host will start the auction shortly</p>
            </div>
        `;
        }
        timer.stop();
    }
}

async function handlePlayerSold(data) {
    timer.stop();
    currentPlayerId = 0;
    justSold = true;

    // Show SOLD animation on the current player panel
    const panel = document.getElementById('currentPlayerPanel');
    if (panel) {
        if (data.teamId === currentTeamId) {
            panel.innerHTML = `
                <div class="text-center p-4 sold-animation">
                    <h1 style="font-size:3rem;font-weight:900;color:#10b981;margin-bottom:0.5rem">YOU WON!</h1>
                    <h3 style="font-weight:700;margin-bottom:0.25rem">${escapeHtml(data.playerName)}</h3>
                    <h5 style="color:var(--gray-500);margin-bottom:1rem">for ${data.soldPrice} points</h5>
                </div>
            `;
        } else {
            panel.innerHTML = `
                <div class="text-center p-4 sold-animation">
                    <h1 style="font-size:3rem;font-weight:900;color:#ef4444;margin-bottom:0.5rem">SOLD!</h1>
                    <h3 style="font-weight:700;margin-bottom:0.25rem">${escapeHtml(data.playerName)}</h3>
                    <h5 style="color:var(--gray-500);margin-bottom:1rem">to ${escapeHtml(data.teamName)} for ${data.soldPrice} points</h5>
                </div>
            `;
        }
    }

    if (data.teamId === currentTeamId) {
        ToastManager.show(`You won ${data.playerName} for ${data.soldPrice} points!`, 'success', 5000);
        // Add to my team list
        const teamList = document.getElementById('myTeamList');
        if (teamList) {
            // Remove "no players yet" message if present
            const noPlayersMsg = document.getElementById('noPlayersMsg');
            if (noPlayersMsg) noPlayersMsg.remove();

            const item = document.createElement('div');
            item.className = 'col-md-6 mb-2';
            item.innerHTML = `
                <div class="player-card-auction sold-animation">
                    <div style="display:flex;justify-content:space-between;align-items:center">
                        <div>
                            <strong>${escapeHtml(data.playerName)}</strong><br>
                            <small style="color:var(--gray-500)">${escapeHtml(data.position || '')}</small>
                        </div>
                        <span class="badge-auction badge-success">${data.soldPrice} pts</span>
                    </div>
                </div>
            `;
            teamList.prepend(item);
        }

        // Update the My Team header count
        const teamHeader = document.querySelector('.card-header-success span');
        if (teamHeader) {
            const match = teamHeader.textContent.match(/My Team \((\d+) players?\)/);
            if (match) {
                const newCount = parseInt(match[1]) + 1;
                teamHeader.textContent = `My Team (${newCount} player${newCount !== 1 ? 's' : ''})`;
            }
        }
    } else {
        ToastManager.show(`${data.playerName} sold to ${data.teamName} for ${data.soldPrice} points`, 'info', 4000);
    }

    // Update points display
    const pointsDisplay = document.getElementById('pointsDisplay');
    if (pointsDisplay && data.teamId === currentTeamId) {
        const currentPoints = parseInt(pointsDisplay.textContent) || 0;
        pointsDisplay.textContent = currentPoints - data.soldPrice;
    }

    // Update player count display
    const playerCountEls = document.querySelectorAll('.stat-value');
    if (playerCountEls.length >= 2 && data.teamId === currentTeamId) {
        const countText = playerCountEls[1].textContent; // "0 / 11" format
        const parts = countText.split(' / ');
        if (parts.length === 2) {
            playerCountEls[1].textContent = `${parseInt(parts[0]) + 1} / ${parts[1]}`;
        }
    }
}

function handlePauseUpdate(isPaused) {
    const badge = document.getElementById('statusBadge');
    if (badge) {
        badge.className = `status-badge ${isPaused ? 'status-paused' : 'status-active'}`;
        badge.textContent = isPaused ? 'Paused' : 'Active';
    }
    const bidBtn = document.getElementById('bidButton');
    if (bidBtn) bidBtn.disabled = isPaused;
}

function handleTeamUpdate(data) {
    if (data.teamId === currentTeamId) {
        const pointsDisplay = document.getElementById('pointsDisplay');
        if (pointsDisplay && data.remainingPoints != null) {
            pointsDisplay.textContent = data.remainingPoints;
        }
    }
}

function handleAuctionComplete(message) {
    ToastManager.show(message, 'success', 8000);
}

function addBidToHistory(teamName, bidAmount) {
    const historyList = document.getElementById('bidHistoryList');
    if (!historyList) return;
    const item = document.createElement('div');
    item.className = 'bid-history-item';
    item.innerHTML = `
        <span style="font-weight:600">${escapeHtml(teamName)}</span>
        <span class="badge-auction badge-primary">${bidAmount} pts</span>
    `;
    historyList.prepend(item);
    while (historyList.children.length > 10) {
        historyList.removeChild(historyList.lastChild);
    }
}

// ─── Actions ───

async function placeBid() {
    const input = document.getElementById('bidAmount');
    const errorEl = document.getElementById('bidError');
    if (!input) return;

    const amount = parseInt(input.value);
    if (!amount || amount <= 0) {
        if (errorEl) {
            errorEl.style.display = 'block';
            errorEl.innerHTML = '<div class="field-error">Please enter a valid bid amount</div>';
        }
        return;
    }

    if (!currentPlayerId || currentPlayerId === 0) {
        ToastManager.show('No player is currently in auction', 'warning');
        return;
    }

    // Disable button while processing
    const bidBtn = document.getElementById('bidButton');
    if (bidBtn) bidBtn.disabled = true;

    try {
        const result = await conn.post('/Auction/PlaceBid', {
            lobbyId: currentLobbyId,
            playerId: currentPlayerId,
            teamId: currentTeamId,
            bidAmount: amount
        });

        if (result.success) {
            input.value = '';
            if (errorEl) errorEl.style.display = 'none';
            ToastManager.show('Bid placed!', 'success', 2000);
        } else {
            if (errorEl) {
                errorEl.style.display = 'block';
                errorEl.innerHTML = `<div class="field-error">${escapeHtml(result.message || 'Bid failed')}</div>`;
            }
            if (bidBtn) bidBtn.disabled = false;
            ToastManager.show(result.message || 'Bid failed', 'error');
        }
    } catch (err) {
        console.error('placeBid error:', err);
        if (bidBtn) bidBtn.disabled = false;
        ToastManager.show('Failed to place bid. Check your connection.', 'error');
    }
}

function quickBid(increment) {
    const input = document.getElementById('bidAmount');
    if (!input) return;
    const currentMin = parseInt(input.min) || 0;
    input.value = currentMin > 0 ? currentMin + increment : increment;
}

async function fetchAuctionState() {
    try {
        const response = await fetch(`/Auction/AuctionState?lobbyId=${currentLobbyId}`);
        const state = await response.json();
        if (state.currentPlayer) {
            currentPlayerId = state.currentPlayer.playerId;
        }
    } catch (err) {
        console.error('Failed to fetch auction state:', err);
    }
}