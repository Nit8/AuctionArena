// AuctionArena - Viewer Dashboard Logic (Spectator Mode)

let conn;
let timer;
let currentLobbyId;
let currentPlayerId = 0;
let pointsPerTeam = 1000;
let justSold = false;

async function initViewerDashboard(lobbyId, initialPointsPerTeam) {
    currentLobbyId = lobbyId;
    pointsPerTeam = initialPointsPerTeam;

    conn = new AuctionConnection('/auctionHub');

    conn.on('bidUpdate', handleBidUpdate);
    conn.on('playerUpdate', handlePlayerUpdate);
    conn.on('playerSold', handlePlayerSold);
    conn.on('pauseUpdate', handlePauseUpdate);
    conn.on('teamUpdate', handleTeamUpdate);
    conn.on('auctionComplete', handleAuctionComplete);
    conn.on('saleRevoked', handleSaleRevoked);
    conn.on('bidReset', handleBidReset);
    conn.on('auctionReactivated', handleAuctionReactivated);
    conn.on('timerUpdate', handleTimerUpdate);
    conn.on('reconnected', () => { fetchViewerState(); });

    await conn.connect(lobbyId);

    timer = new CountdownTimer(null, 30);

    // Fetch current state on first connect
    await fetchViewerState();
}

function handleBidUpdate(data) {
    const bidDisplay = document.getElementById('bidDisplay');
    if (bidDisplay) {
        bidDisplay.innerHTML = `
            <div class="viewer-bid-amount">${escapeHtml(data.bidAmount.toString())}</div>
            <div class="viewer-bid-team">by ${escapeHtml(data.teamName)}</div>
        `;
        bidDisplay.classList.add('bid-animation');
        setTimeout(() => bidDisplay.classList.remove('bid-animation'), 400);
    }

    // Reset countdown
    timer.reset();

    // Add to bid feed
    addBidToFeed(data.teamName, data.bidAmount);
}

function handlePlayerUpdate(data) {
    const panel = document.getElementById('heroAuctionPanel');
    if (!panel) return;

    if (data.playerId) {
        currentPlayerId = data.playerId;
        justSold = false;
        panel.innerHTML = `
            <div class="viewer-hero-content">
                <div class="viewer-hero-position">${escapeHtml(data.position)}</div>
                <div class="viewer-hero-name">${escapeHtml(data.playerName)}</div>
                <div class="viewer-hero-bid" id="bidDisplay">
                    <div class="viewer-bid-waiting">Waiting for bids...</div>
                </div>
            </div>
        `;
        timer.start();
    } else {
        if (justSold) {
            justSold = false;
            setTimeout(() => {
                currentPlayerId = 0;
                panel.innerHTML = `
                    <div class="viewer-hero-empty">
                        <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" style="opacity:0.3">
                            <path d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"/>
                        </svg>
                        <div style="font-size:1.25rem;font-weight:600;opacity:0.5;margin-top:1rem">No player in auction</div>
                        <div style="font-size:0.9rem;opacity:0.3">Waiting for host to start</div>
                    </div>
                `;
            }, 2500);
        } else {
            currentPlayerId = 0;
            panel.innerHTML = `
                <div class="viewer-hero-empty">
                    <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" style="opacity:0.3">
                        <path d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"/>
                    </svg>
                    <div style="font-size:1.25rem;font-weight:600;opacity:0.5;margin-top:1rem">No player in auction</div>
                    <div style="font-size:0.9rem;opacity:0.3">Waiting for host to start</div>
                </div>
            `;
        }
        timer.stop();
    }
}

function handlePlayerSold(data) {
    timer.stop();
    currentPlayerId = 0;
    justSold = true;

    const panel = document.getElementById('heroAuctionPanel');
    if (panel) {
        panel.innerHTML = `
            <div class="viewer-hero-content sold-animation">
                <div class="viewer-sold-badge">SOLD!</div>
                <div class="viewer-hero-name" style="font-size:2.5rem">${escapeHtml(data.playerName)}</div>
                <div class="viewer-sold-team">to ${escapeHtml(data.teamName)}</div>
                <div class="viewer-bid-amount" style="color:#10b981">${data.soldPrice} pts</div>
            </div>
        `;
    }

    // Update sold players list
    const soldList = document.getElementById('soldPlayersList');
    if (soldList) {
        const emptyState = soldList.querySelector('.viewer-empty-state');
        if (emptyState) emptyState.remove();

        const item = document.createElement('div');
        item.className = 'viewer-player-item viewer-player-sold';
        item.setAttribute('data-player-id', data.playerId);
        item.innerHTML = `
            <div class="viewer-player-info">
                <div class="viewer-player-name">${escapeHtml(data.playerName)}</div>
                <div class="viewer-player-meta">
                    <span class="viewer-player-position">${escapeHtml(data.position || '')}</span>
                    <span class="viewer-player-buyer">${escapeHtml(data.teamName)}</span>
                </div>
            </div>
            <div class="viewer-player-price">${data.soldPrice} pts</div>
        `;
        soldList.prepend(item);

        const soldBadge = document.getElementById('soldCountBadge');
        if (soldBadge) soldBadge.textContent = parseInt(soldBadge.textContent) + 1;

        const soldCount = document.getElementById('soldCount');
        if (soldCount) soldCount.textContent = parseInt(soldCount.textContent) + 1;
    }

    // Remove from available
    const availItem = document.querySelector(`#availablePlayersList [data-player-id="${data.playerId}"]`);
    if (availItem) availItem.remove();
    const availBadge = document.getElementById('availableCountBadge');
    if (availBadge) availBadge.textContent = Math.max(0, parseInt(availBadge.textContent) - 1);
    const availCount = document.getElementById('availableCount');
    if (availCount) availCount.textContent = Math.max(0, parseInt(availCount.textContent) - 1);

    // Update total spent
    const totalSpentEl = document.getElementById('totalSpent');
    if (totalSpentEl) totalSpentEl.textContent = parseInt(totalSpentEl.textContent) + data.soldPrice;

    // Update team display
    updateTeamDisplay(data.teamId, data.teamName);
}

function handlePauseUpdate(isPaused) {
    const badge = document.getElementById('statusBadge');
    const text = document.getElementById('statusText');
    if (badge && text) {
        badge.className = `viewer-status-badge ${isPaused ? 'paused' : 'live'}`;
        text.textContent = isPaused ? 'PAUSED' : 'LIVE';
    }
}

function handleTeamUpdate(data) {
    const el = document.querySelector(`[data-team-id="${data.teamId}"]`);
    if (el) {
        const pointsEl = el.querySelector('.viewer-budget-points');
        if (pointsEl && data.remainingPoints != null) {
            pointsEl.textContent = `${data.remainingPoints} pts`;
        }
        // Update progress bar
        const fillEl = el.querySelector('.viewer-budget-fill');
        if (fillEl && pointsPerTeam > 0) {
            const percent = Math.round((data.remainingPoints / pointsPerTeam) * 100);
            fillEl.style.width = `${percent}%`;
        }
    }
}

function handleAuctionComplete(message) {
    const badge = document.getElementById('statusBadge');
    const text = document.getElementById('statusText');
    if (badge && text) {
        badge.className = 'viewer-status-badge ended';
        text.textContent = 'ENDED';
    }
}

function handleSaleRevoked(data) {
    // Move player back to available list
    const soldItem = document.querySelector(`#soldPlayersList [data-player-id="${data.playerId}"]`);
    if (soldItem) {
        soldItem.remove();
        const soldBadge = document.getElementById('soldCountBadge');
        if (soldBadge) soldBadge.textContent = Math.max(0, parseInt(soldBadge.textContent) - 1);
        const soldCount = document.getElementById('soldCount');
        if (soldCount) soldCount.textContent = Math.max(0, parseInt(soldCount.textContent) - 1);
    }

    // Add back to available
    const availList = document.getElementById('availablePlayersList');
    if (availList) {
        const emptyState = availList.querySelector('.viewer-empty-state');
        if (emptyState) emptyState.remove();

        const item = document.createElement('div');
        item.className = 'viewer-player-item';
        item.setAttribute('data-player-id', data.playerId);
        item.innerHTML = `
            <div class="viewer-player-name">${escapeHtml(data.playerName)}</div>
            <div class="viewer-player-position"></div>
        `;
        availList.prepend(item);

        const availBadge = document.getElementById('availableCountBadge');
        if (availBadge) availBadge.textContent = parseInt(availBadge.textContent) + 1;
        const availCount = document.getElementById('availableCount');
        if (availCount) availCount.textContent = parseInt(availCount.textContent) + 1;
    }

    // Update total spent
    const totalSpentEl = document.getElementById('totalSpent');
    if (totalSpentEl) totalSpentEl.textContent = Math.max(0, parseInt(totalSpentEl.textContent) - data.refundAmount);

    // Update team
    updateTeamDisplay(data.teamId, data.teamName);

    // Update status back to active if it was ended
    const badge = document.getElementById('statusBadge');
    const text = document.getElementById('statusText');
    if (badge && text && text.textContent === 'ENDED') {
        badge.className = 'viewer-status-badge live';
        text.textContent = 'LIVE';
    }
}

function handleBidReset(data) {
    // Reset the bid display to show current player with no bids
    const bidDisplay = document.getElementById('bidDisplay');
    if (bidDisplay) {
        bidDisplay.innerHTML = `<div class="viewer-bid-waiting">Bids reset - Waiting for bids...</div>`;
    }
    // Clear bid feed
    const bidFeed = document.getElementById('bidFeedList');
    if (bidFeed) {
        bidFeed.innerHTML = `<div class="viewer-empty-state">Bids have been reset</div>`;
    }
    // Restart timer
    timer.start();
}

function handleAuctionReactivated() {
    const badge = document.getElementById('statusBadge');
    const text = document.getElementById('statusText');
    if (badge && text) {
        badge.className = 'viewer-status-badge live';
        text.textContent = 'LIVE';
    }
}

function handleTimerUpdate(durationSeconds) {
    timer.setDuration(durationSeconds);
    timer.reset();
}

async function updateTeamDisplay(teamId, teamName) {
    try {
        const response = await fetch(`/Auction/ViewerDashboardData?lobbyId=${currentLobbyId}`);
        const state = await response.json();
        const team = state.teams?.find(t => t.teamId === teamId);
        if (team) {
            const el = document.querySelector(`[data-team-id="${teamId}"]`);
            if (el) {
                const pointsEl = el.querySelector('.viewer-budget-points');
                if (pointsEl) pointsEl.textContent = `${team.remainingPoints} pts`;
                const fillEl = el.querySelector('.viewer-budget-fill');
                if (fillEl && pointsPerTeam > 0) {
                    fillEl.style.width = `${Math.round((team.remainingPoints / pointsPerTeam) * 100)}%`;
                }
                const metaEl = el.querySelector('.viewer-budget-meta span');
                if (metaEl) metaEl.textContent = `${team.playerCount} players`;
            }
        }
    } catch (err) {
        console.error('Failed to update team display:', err);
    }
}

function addBidToFeed(teamName, bidAmount) {
    const feedList = document.getElementById('bidFeedList');
    if (!feedList) return;

    const emptyState = feedList.querySelector('.viewer-empty-state');
    if (emptyState) emptyState.remove();

    const item = document.createElement('div');
    item.className = 'viewer-bid-feed-item';
    item.innerHTML = `
        <div class="viewer-bid-feed-team">${escapeHtml(teamName)}</div>
        <div class="viewer-bid-feed-amount">${bidAmount} pts</div>
    `;
    feedList.prepend(item);
    // Keep only last 20
    while (feedList.children.length > 20) {
        feedList.removeChild(feedList.lastChild);
    }
}

async function fetchViewerState() {
    try {
        const response = await fetch(`/Auction/ViewerDashboardData?lobbyId=${currentLobbyId}`);
        const state = await response.json();
        if (state.currentPlayer) {
            currentPlayerId = state.currentPlayer.playerId;
        }
        // Update timer duration
        if (state.timerDuration) {
            timer.setDuration(state.timerDuration);
        }
        // Update status badge
        const badge = document.getElementById('statusBadge');
        const text = document.getElementById('statusText');
        if (badge && text) {
            if (!state.isActive) {
                badge.className = 'viewer-status-badge ended';
                text.textContent = 'ENDED';
            } else if (state.isPaused) {
                badge.className = 'viewer-status-badge paused';
                text.textContent = 'PAUSED';
            } else {
                badge.className = 'viewer-status-badge live';
                text.textContent = 'LIVE';
            }
        }
    } catch (err) {
        console.error('Failed to fetch viewer state:', err);
    }
}
