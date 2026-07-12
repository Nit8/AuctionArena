// AuctionArena - Team Dashboard Logic

let conn;
let timer;
let currentLobbyId;
let currentTeamId;
let currentPlayerId = 0;
let currentHighestBid = 0;          // tracks the latest highest bid (0 when no bid yet)
let currentMinimumBid = 0;          // host-set minimum bid for the current player
let currentBidIncrement = 0;        // lobby-wide minimum bid increment (set by host)
let customIncrement = 0;            // team-owner's personal quick-bid increment (persisted to localStorage)
let justSold = false;

// localStorage key for persisting the team's custom increment per team
function customIncrementStorageKey() {
    return `auctionarena:customIncrement:lobby=${currentLobbyId}:team=${currentTeamId}`;
}

function loadCustomIncrement() {
    try {
        const stored = localStorage.getItem(customIncrementStorageKey());
        const val = stored ? parseInt(stored) : 0;
        customIncrement = (isNaN(val) || val < 0) ? 0 : val;
    } catch {
        customIncrement = 0;
    }
    return customIncrement;
}

function saveCustomIncrement(value) {
    customIncrement = Math.max(0, parseInt(value) || 0);
    try {
        localStorage.setItem(customIncrementStorageKey(), String(customIncrement));
    } catch {
        // localStorage may be unavailable (private mode); fail silently
    }
}

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
    conn.on('availablePlayersUpdate', handleAvailablePlayersUpdate);
    conn.on('reconnected', () => { fetchAuctionState(); });
    conn.on('teamSuspension', handleTeamSuspension);
    conn.on('bidIncrementUpdate', handleBidIncrementUpdate);

    await conn.connect(lobbyId);

    // Load team's saved custom increment from localStorage
    loadCustomIncrement();
    syncCustomIncrementUI();

    // Fetch current auction state on first connect (in case we joined mid-auction)
    if (!initialPlayerId) {
        await fetchAuctionState();
    }

    timer = new CountdownTimer(null, 30);
}

function handleBidUpdate(data) {
    currentHighestBid = parseInt(data.bidAmount) || 0;
    const display = document.getElementById('currentBidDisplay');
    if (display) {
        display.innerHTML = `
            <div class="bid-amount">${escapeHtml(data.bidAmount.toString())}</div>
            <div class="bid-team">by ${escapeHtml(data.teamName)}</div>
        `;
        display.classList.add('bid-animation');
        setTimeout(() => display.classList.remove('bid-animation'), 400);
    }

    // Update bid input minimum to currentBid + 1 (server still validates)
    const bidInput = document.getElementById('bidAmount');
    if (bidInput) bidInput.min = currentHighestBid + 1;

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
        currentHighestBid = 0; // reset; will be repopulated by fetchAuctionState if mid-auction
        currentMinimumBid = parseInt(data.minimumBid) || 0;
        justSold = false;

        // Remove the newly-auctioned player from the Available Players list
        const availItem = document.querySelector(`#availablePlayersList [data-player-id="${data.playerId}"]`);
        if (availItem) availItem.remove();
        const availCount = document.getElementById('availableCount');
        if (availCount) availCount.textContent = Math.max(0, parseInt(availCount.textContent) - 1);

        const minBidHint = currentMinimumBid > 0
            ? `<div class="badge-auction badge-info" style="margin-bottom:0.75rem;padding:0.4rem 0.75rem">Minimum bid: ${currentMinimumBid} pts</div>`
            : '';
        const inputMin = currentMinimumBid > 0 ? currentMinimumBid : 1;

        panel.innerHTML = `
            <div class="text-center p-4">
                <h2 style="font-weight:800;margin-bottom:0.5rem">${escapeHtml(data.playerName)}</h2>
                <h5 style="color:var(--gray-500);margin-bottom:1.5rem">${escapeHtml(data.position)}</h5>
                ${minBidHint}
                <div class="bid-display" id="currentBidDisplay">
                    <h4 style="color:var(--gray-400);font-weight:600">No bids yet - Place the first bid!</h4>
                </div>
                <div id="countdownTimer" class="countdown-timer mt-3"></div>
                <div style="max-width:500px;margin:1.5rem auto 0">
                    <div style="display:flex;gap:0.5rem;margin-bottom:0.75rem">
                        <input type="number" class="form-control-auction form-control-lg-auction" id="bidAmount"
                               placeholder="Enter bid amount" min="${inputMin}" style="flex:1">
                        <button class="btn-auction btn-success-auction btn-lg-auction" onclick="placeBid()" id="bidButton">
                            Bid
                        </button>
                    </div>
                    <div class="quick-bid-group">
                        <button class="quick-bid-btn" onclick="quickBid(50)" title="Bid current highest + 50">+50</button>
                        <button class="quick-bid-btn" onclick="quickBid(100)" title="Bid current highest + 100">+100</button>
                        <button class="quick-bid-btn" onclick="quickBid(200)" title="Bid current highest + 200">+200</button>
                        <button class="quick-bid-btn" onclick="quickBid(500)" title="Bid current highest + 500">+500</button>
                    </div>
                    <div class="custom-bid-row" style="display:flex;gap:0.5rem;margin-top:0.5rem;align-items:center">
                        <label for="customIncrementInput" style="font-size:0.75rem;color:var(--gray-500);white-space:nowrap">My +</label>
                        <input type="number" id="customIncrementInput" class="form-control-auction"
                               placeholder="e.g. 75" min="0" style="flex:1;padding:0.35rem 0.6rem;font-size:0.85rem"
                               value="${customIncrement > 0 ? customIncrement : ''}"
                               onchange="onCustomIncrementChange(this.value)" />
                        <button class="quick-bid-btn" id="customBidBtn"
                                onclick="quickBid(customIncrement)"
                                title="Use my saved increment">
                            +My Inc
                        </button>
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
                currentHighestBid = 0;
                currentMinimumBid = 0;
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
            currentHighestBid = 0;
            currentMinimumBid = 0;
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

function onCustomIncrementChange(value) {
    saveCustomIncrement(value);
    // Update the button title to reflect the new value
    const btn = document.getElementById('customBidBtn');
    if (btn) {
        btn.title = customIncrement > 0
            ? `Bid current highest + ${customIncrement}`
            : 'Set a positive number above first';
    }
    if (customIncrement > 0) {
        ToastManager.show(`Custom increment saved: +${customIncrement}`, 'success', 1500);
    }
}

function syncCustomIncrementUI() {
    const input = document.getElementById('customIncrementInput');
    if (input) {
        input.value = customIncrement > 0 ? customIncrement : '';
    }
    const btn = document.getElementById('customBidBtn');
    if (btn) {
        btn.title = customIncrement > 0
            ? `Bid current highest + ${customIncrement}`
            : 'Set a positive number above first';
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

    // Remove the sold player from the local Available Players list (server will also push availablePlayersUpdate)
    const availItem = document.querySelector(`#availablePlayersList [data-player-id="${data.playerId}"]`);
    if (availItem) availItem.remove();
    const availCount = document.getElementById('availableCount');
    if (availCount) availCount.textContent = Math.max(0, parseInt(availCount.textContent) - 1);
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

function handleTeamSuspension(data) {
    if (data.teamId === currentTeamId) {
        if (data.isSuspended) {
            ToastManager.show('Your team has been suspended from bidding by the host.', 'error', 6000);
            const bidBtn = document.getElementById('bidButton');
            if (bidBtn) bidBtn.disabled = true;
        } else {
            ToastManager.show('Your team has been unsuspended! You can bid again.', 'success', 4000);
        }
    }
}

function handleBidIncrementUpdate(bidIncrement) {
    currentBidIncrement = parseInt(bidIncrement) || 0;
    if (currentBidIncrement > 0) {
        ToastManager.show(`Minimum bid increment is now +${currentBidIncrement}`, 'info', 3000);
    }
}

function renderAvailablePlayers(players) {
    const list = document.getElementById('availablePlayersList');
    const countEl = document.getElementById('availableCount');
    if (!list) return;

    // Exclude any player that is currently in auction on this client
    const filtered = (players || []).filter(p => p.playerId !== currentPlayerId);

    list.innerHTML = '';
    if (!filtered.length) {
        list.innerHTML = '<p class="text-center text-muted p-3 mb-0">No available players</p>';
    } else {
        for (const p of filtered) {
            const item = document.createElement('div');
            item.className = 'player-card-auction';
            item.setAttribute('data-player-id', p.playerId);
            item.innerHTML = `
                <div style="display:flex;justify-content:space-between;align-items:center">
                    <div>
                        <strong style="font-size:0.9rem">${escapeHtml(p.playerName)}</strong><br>
                        <small style="color:var(--gray-500)">${escapeHtml(p.position || '')}</small>
                    </div>
                </div>
            `;
            list.appendChild(item);
        }
    }
    if (countEl) countEl.textContent = filtered.length;
}

function handleAvailablePlayersUpdate(data) {
    if (!data || !data.players) return;
    renderAvailablePlayers(data.players);
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

// quickBid(increment) — sets the bid input to (current bid OR minimum bid) + increment.
// - If there's already a bid on the player: newValue = currentHighestBid + increment
// - Else if host set a minimum bid:           newValue = minimumBid + increment
// - Else:                                     newValue = increment
// The lobby-wide minimum increment (set by host) acts as a floor for the increment.
function quickBid(increment) {
    const input = document.getElementById('bidAmount');
    if (!input) return;

    const inc = Math.max(parseInt(increment) || 0, currentBidIncrement || 0);
    let base;
    if (currentHighestBid > 0) {
        base = currentHighestBid;
    } else if (currentMinimumBid > 0) {
        base = currentMinimumBid;
    } else {
        base = 0;
    }
    input.value = base + inc;
    // Visual feedback
    input.classList.add('bid-animation');
    setTimeout(() => input.classList.remove('bid-animation'), 300);
}

async function fetchAuctionState() {
    try {
        const response = await fetch(`/Auction/AuctionState?lobbyId=${currentLobbyId}`);
        const state = await response.json();
        if (state.currentPlayer) {
            currentPlayerId = state.currentPlayer.playerId;
            currentHighestBid = parseInt(state.currentHighestBid) || 0;
            currentMinimumBid = parseInt(state.minimumBid) || 0;
            // Merge minimumBid into the data passed to handlePlayerUpdate so the panel renders correctly
            state.currentPlayer.minimumBid = state.minimumBid;
            state.currentPlayer.currentHighestBid = state.currentHighestBid;
            handlePlayerUpdate(state.currentPlayer);
            // If there's already a highest bid, also reflect it in the display + input min
            if (currentHighestBid > 0) {
                const display = document.getElementById('currentBidDisplay');
                if (display) {
                    display.innerHTML = `
                        <div class="bid-amount">${escapeHtml(String(currentHighestBid))}</div>
                        <div class="bid-team">by ${escapeHtml(state.currentHighestBidder || '?')}</div>
                    `;
                }
                const bidInput = document.getElementById('bidAmount');
                if (bidInput) bidInput.min = currentHighestBid + 1;
            }
        }
        if (state.availablePlayers) {
            renderAvailablePlayers(state.availablePlayers);
        }
    } catch (err) {
        console.error('Failed to fetch auction state:', err);
    }
}