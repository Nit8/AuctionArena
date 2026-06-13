// AuctionArena - Viewer Dashboard Logic (Spectator Mode)

let conn;
let timer;
let currentLobbyId;
let currentPlayerId = 0;
let currentPlayerName = '';
let currentPosition = '';
let pointsPerTeam = 1000;
let justSold = false;

async function initViewerDashboard(lobbyId, initialPointsPerTeam) {
    console.log('🟢 initViewerDashboard started for lobby:', lobbyId);
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
    
    console.log('🟢 Registering availablePlayersUpdate handler');
    conn.on('availablePlayersUpdate', (data) => {
        console.log('🟢 availablePlayersUpdate received:', data);
        handleAvailablePlayersUpdate(data);
    });

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
        // If there was a previous player in auction (not sold), add it back to available first
        // This handles the case where the host skips a player and immediately starts another
        if (currentPlayerId && currentPlayerId !== data.playerId && !justSold) {
            addPlayerBackToAvailable(currentPlayerId, currentPlayerName, currentPosition);
        }

        currentPlayerId = data.playerId;
        currentPlayerName = data.playerName || '';

        currentPosition = data.position || '';
        justSold = false;

        // Remove the player from the available list since they are now being auctioned
        const availItem = document.querySelector(`#availablePlayersList [data-player-id="${data.playerId}"]`);
        if (availItem) {
            availItem.remove();
            const availBadge = document.getElementById('availableCountBadge');
            if (availBadge) availBadge.textContent = Math.max(0, parseInt(availBadge.textContent) - 1);
            const availCount = document.getElementById('availableCount');
            if (availCount) availCount.textContent = Math.max(0, parseInt(availCount.textContent) - 1);
        }

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
        // No player in auction — if there was a player being auctioned that wasn't sold,
        // add it back to the available list (e.g., player was skipped)
        if (currentPlayerId && !justSold) {
            addPlayerBackToAvailable(currentPlayerId, currentPlayerName, currentPosition);
        }

        if (justSold) {
            justSold = false;
            setTimeout(() => {
                currentPlayerId = 0;
                currentPlayerName = '';
                currentPosition = '';
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
            currentPlayerName = '';
            currentPosition = '';
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

    // Remove from available list (in case it wasn't removed during handlePlayerUpdate)
    const availItem = document.querySelector(`#availablePlayersList [data-player-id="${data.playerId}"]`);
    if (availItem) {
        availItem.remove();
        const availBadge = document.getElementById('availableCountBadge');
        if (availBadge) availBadge.textContent = Math.max(0, parseInt(availBadge.textContent) - 1);
        const availCount = document.getElementById('availableCount');
        if (availCount) availCount.textContent = Math.max(0, parseInt(availCount.textContent) - 1);
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

    // Update total spent
    const totalSpentEl = document.getElementById('totalSpent');
    if (totalSpentEl) totalSpentEl.textContent = parseInt(totalSpentEl.textContent) + data.soldPrice;

    // Update team display
    updateTeamDisplay(data.teamId, data.teamName);

    // Add player to team roster
    addPlayerToRoster(data.teamId, data.playerId, data.playerName, data.position, data.soldPrice);
}

function addPlayerToRoster(teamId, playerId, playerName, position, soldPrice) {
    const roster = document.getElementById(`roster-${teamId}`);
    if (!roster) return;

    // Remove empty state if present
    const emptyState = roster.querySelector('.roster-empty');
    if (emptyState) emptyState.remove();

    // Check if player already exists
    const existing = roster.querySelector(`[data-player-id="${playerId}"]`);
    if (existing) return;

    const item = document.createElement('div');
    item.className = 'roster-player-item';
    item.setAttribute('data-player-id', playerId);
    item.innerHTML = `
        <div class="roster-player-info">
            <span class="roster-player-name">${escapeHtml(playerName)}</span>
            <span class="roster-player-position">${escapeHtml(position || '')}</span>
        </div>
        <span class="roster-player-price">${soldPrice}</span>
    `;
    roster.appendChild(item);

    // Update team roster header count
    const rosterCard = roster.closest('.team-roster-card');
    if (rosterCard) {
        const countEl = rosterCard.querySelector('.team-roster-count');
        if (countEl) {
            const current = parseInt(countEl.textContent) || 0;
            countEl.textContent = `${current + 1} players`;
        }
    }
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
        const pointsEl = el.querySelector('.team-budget-pts');
        if (pointsEl && data.remainingPoints != null) {
            pointsEl.textContent = data.remainingPoints;
        }
        // Update progress bar
        const fillEl = el.querySelector('.team-budget-card-fill');
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
    // Move player from sold list back to available list
    const soldItem = document.querySelector(`#soldPlayersList [data-player-id="${data.playerId}"]`);
    if (soldItem) {
        soldItem.remove();
        const soldBadge = document.getElementById('soldCountBadge');
        if (soldBadge) soldBadge.textContent = Math.max(0, parseInt(soldBadge.textContent) - 1);
        const soldCount = document.getElementById('soldCount');
        if (soldCount) soldCount.textContent = Math.max(0, parseInt(soldCount.textContent) - 1);
    }

    // Add back to available list with proper layout (use position from event data)
    addPlayerBackToAvailable(data.playerId, data.playerName, data.position || '');

    // Update total spent
    const totalSpentEl = document.getElementById('totalSpent');
    if (totalSpentEl) totalSpentEl.textContent = Math.max(0, parseInt(totalSpentEl.textContent) - data.refundAmount);

    // Update team
    updateTeamDisplay(data.teamId, data.teamName);

    // Remove player from team roster
    removePlayerFromRoster(data.teamId, data.playerId);

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
        const team = state.teams?.find(t => t.teamId == teamId);
        if (team) {
            const el = document.querySelector(`[data-team-id="${teamId}"]`);
            if (el) {
                const pointsEl = el.querySelector('.team-budget-pts');
                if (pointsEl) pointsEl.textContent = team.remainingPoints;
                const fillEl = el.querySelector('.team-budget-card-fill');
                if (fillEl && pointsPerTeam > 0) {
                    fillEl.style.width = `${Math.round((team.remainingPoints / pointsPerTeam) * 100)}%`;
                }
                const footer = el.querySelector('.team-budget-card-footer');
                if (footer) {
                    const playersEl = footer.querySelector('.team-budget-players');
                    if (playersEl) playersEl.innerHTML = `
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>
                        ${team.playerCount}
                    `;
                    const spentEl = footer.querySelector('.team-budget-spent');
                    if (spentEl) spentEl.textContent = `spent ${pointsPerTeam - team.remainingPoints}`;
                }
            }
        }
    } catch (err) {
        console.error('Failed to update team display:', err);
    }
}

function removePlayerFromRoster(teamId, playerId) {
    const roster = document.getElementById(`roster-${teamId}`);
    if (!roster) return;

    const item = roster.querySelector(`[data-player-id="${playerId}"]`);
    if (item) {
        item.remove();
        // Show empty state if no players left
        if (roster.children.length === 0) {
            roster.innerHTML = '<div class="roster-empty">No players yet</div>';
        }
    }

    // Update team roster header count
    const rosterCard = roster.closest('.team-roster-card');
    if (rosterCard) {
        const countEl = rosterCard.querySelector('.team-roster-count');
        if (countEl) {
            const current = Math.max(0, parseInt(countEl.textContent) || 0);
            countEl.textContent = `${current - 1} players`;
        }
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

// Helper: Add a player back to the available players list
function addPlayerBackToAvailable(playerId, playerName, position) {
    const availList = document.getElementById('availablePlayersList');
    if (!availList) return;

    // Don't add duplicate
    const existing = availList.querySelector(`[data-player-id="${playerId}"]`);
    if (existing) return;

    const emptyState = availList.querySelector('.viewer-empty-state');
    if (emptyState) emptyState.remove();

    const item = document.createElement('div');
    item.className = 'viewer-player-item';
    item.setAttribute('data-player-id', playerId);
    item.innerHTML = `
        <div class="viewer-player-name">${escapeHtml(playerName)}</div>
        <div class="viewer-player-position">${escapeHtml(position)}</div>
    `;
    availList.prepend(item);

    const availBadge = document.getElementById('availableCountBadge');
    if (availBadge) availBadge.textContent = parseInt(availBadge.textContent) + 1;
    const availCount = document.getElementById('availableCount');
    if (availCount) availCount.textContent = parseInt(availCount.textContent) + 1;
}



// Handler for available players list updates from SignalR
function handleAvailablePlayersUpdate(data) {
    console.log('🟢 handleAvailablePlayersUpdate called with data:', data);
    const availList = document.getElementById('availablePlayersList');
    console.log('🟢 availList element:', availList);
    
    if (!availList) {
        console.warn('🔴 availablePlayersList element not found!');
        return;
    }

    // Clear the current list
    availList.innerHTML = '';
    console.log('🟢 Cleared available players list');

    // Repopulate with new data
    if (data.players && data.players.length > 0) {
        console.log('🟢 Adding', data.players.length, 'players to list');
        data.players.forEach(player => {
            const item = document.createElement('div');
            item.className = 'viewer-player-item';
            item.setAttribute('data-player-id', player.playerId);
            item.innerHTML = `
                <div class="viewer-player-name">${escapeHtml(player.playerName)}</div>
                <div class="viewer-player-position">${escapeHtml(player.position || '')}</div>
            `;
            availList.appendChild(item);
        });

        // Update badges/counts
        const availBadge = document.getElementById('availableCountBadge');
        if (availBadge) availBadge.textContent = data.players.length;
        const availCount = document.getElementById('availableCount');
        if (availCount) availCount.textContent = data.players.length;
        console.log('🟢 Updated available players count to:', data.players.length);
    } else {
        console.log('🟢 No players available');
        // Show empty state
        availList.innerHTML = '<div class="viewer-empty-state">No players available</div>';
        const availBadge = document.getElementById('availableCountBadge');
        if (availBadge) availBadge.textContent = '0';
        const availCount = document.getElementById('availableCount');
        if (availCount) availCount.textContent = '0';
    }
}

async function fetchViewerState() {
    try {
        const response = await fetch(`/Auction/ViewerDashboardData?lobbyId=${currentLobbyId}`);
        const state = await response.json();
        if (state.currentPlayer) {
            currentPlayerId = state.currentPlayer.playerId;
            currentPlayerName = state.currentPlayer.playerName || '';
            currentPosition = state.currentPlayer.position || '';
            justSold = false;

            // Directly update the hero panel (don't use handlePlayerUpdate to avoid
            // side-effects like removing items from the available list — the server
            // already excludes the current player from RemainingPlayers)
            const panel = document.getElementById('heroAuctionPanel');
            if (panel) {
                panel.innerHTML = `
                    <div class="viewer-hero-content">
                        <div class="viewer-hero-position">${escapeHtml(state.currentPlayer.position)}</div>
                        <div class="viewer-hero-name">${escapeHtml(state.currentPlayer.playerName)}</div>
                        <div class="viewer-hero-bid" id="bidDisplay">
                            ${state.currentHighestBid
                        ? `<div class="viewer-bid-amount">${state.currentHighestBid}</div>
                                   <div class="viewer-bid-team">by ${escapeHtml(state.currentHighestBidder || '')}</div>`
                        : `<div class="viewer-bid-waiting">Waiting for bids...</div>`
                    }
                        </div>
                    </div>
                `;
            }

            timer.start();
        } else {
            // No current player — make sure the hero panel shows the empty state
            currentPlayerId = 0;
            currentPlayerName = '';
            currentPosition = '';
            timer.stop();
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