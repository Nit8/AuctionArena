// AuctionArena - Host Dashboard Logic

let conn;
let timer;
let currentLobbyId;
let currentPlayerId = 0;
let justSold = false; // Flag to delay panel clear after sale

async function initHostDashboard(lobbyId) {
    currentLobbyId = lobbyId;
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
    conn.on('reconnected', () => { fetchAuctionState(); });
    conn.on('teamSuspension', handleTeamSuspension);
    // conn.on('bidIncrementUpdate', handleBidIncrementUpdate);

    await conn.connect(lobbyId);

    // Fetch current auction state on first connect
    await fetchAuctionState();

    timer = new CountdownTimer(() => {
        onTimerExpired();
    }, 30);
}

function handleBidUpdate(data) {
    const bidDisplay = document.getElementById('bidDisplay');
    if (bidDisplay) {
        bidDisplay.innerHTML = `
            <div class="bid-amount">${escapeHtml(data.bidAmount.toString())}</div>
            <div class="bid-team">by ${escapeHtml(data.teamName)}</div>
        `;
        bidDisplay.classList.add('bid-animation');
        setTimeout(() => bidDisplay.classList.remove('bid-animation'), 400);
    }

    // Enable confirm button
    const confirmBtn = document.getElementById('confirmSaleBtn');
    if (confirmBtn) confirmBtn.disabled = false;

    // Reset countdown on new bid
    timer.reset();

    // Update bid history
    addBidToHistory(data.teamName, data.bidAmount);
}

function handlePlayerUpdate(data) {
    const panel = document.getElementById('currentAuctionPanel');
    if (!panel) return;

    if (data.playerId) {
        currentPlayerId = data.playerId;
        justSold = false;
        panel.innerHTML = `
            <div class="text-center p-4">
                <h1 style="font-size:2.5rem;font-weight:800;margin-bottom:0.5rem">${escapeHtml(data.playerName)}</h1>
                <h4 style="color:var(--gray-500);margin-bottom:1.5rem">${escapeHtml(data.position)}</h4>
                <div class="bid-display" id="bidDisplay">
                    <h3 style="color:var(--gray-400);font-weight:600">Waiting for bids...</h3>
                </div>
                <div id="countdownTimer" class="countdown-timer mt-3"></div>
                <div class="d-grid gap-2 mt-4" style="max-width:400px;margin:0 auto">
                    <button id="confirmSaleBtn" class="btn-auction btn-success-auction btn-lg-auction" onclick="confirmSale()" disabled>
                        Confirm Sale
                    </button>
                    <button class="btn-auction btn-danger-auction" onclick="skipPlayer()">
                        Skip Player
                    </button>
                </div>
            </div>
        `;
        timer.start();
    } else {
        // If we just sold a player, keep the SOLD animation briefly before clearing
        if (justSold) {
            justSold = false;
            setTimeout(() => {
                currentPlayerId = 0;
                panel.innerHTML = `
            <div class="text-center p-5">
                <svg style="width:4rem;height:4rem;color:var(--gray-400);margin-bottom:1rem" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"/>
                </svg>
                <h4 style="color:var(--gray-400)">No player in auction</h4>
                <p style="color:var(--gray-400)">Select a player from the list to start bidding</p>
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
                <h4 style="color:var(--gray-400)">No player in auction</h4>
                <p style="color:var(--gray-400)">Select a player from the list to start bidding</p>
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

    // Show SOLD animation on the current auction panel
    const panel = document.getElementById('currentAuctionPanel');
    if (panel) {
        panel.innerHTML = `
            <div class="text-center p-4 sold-animation">
                <h1 style="font-size:3rem;font-weight:900;color:#10b981;margin-bottom:0.5rem">SOLD!</h1>
                <h3 style="font-weight:700;margin-bottom:0.25rem">${escapeHtml(data.playerName)}</h3>
                <h5 style="color:var(--gray-500);margin-bottom:1rem">to ${escapeHtml(data.teamName)}</h5>
                <div class="bid-amount" style="color:#10b981">${data.soldPrice} pts</div>
            </div>
        `;
    }

    ToastManager.show(`${data.playerName} sold to ${data.teamName} for ${data.soldPrice} points!`, 'success', 5000);

    // Update sold players list
    const soldList = document.getElementById('soldPlayersList');
    if (soldList) {
        const count = soldList.children.length + 1;
        const item = document.createElement('div');
        item.className = 'player-card-auction sold';
        item.setAttribute('data-sold-player-id', data.playerId);
        item.innerHTML = `
            <div style="display:flex;justify-content:space-between;align-items:center">
                <div>
                    <strong>${escapeHtml(data.playerName)}</strong><br>
                    <small style="color:var(--gray-500)">${escapeHtml(data.position || '')} - ${escapeHtml(data.teamName)}</small>
                </div>
                <div style="display:flex;align-items:center;gap:0.5rem">
                    <span class="badge-auction badge-success">${data.soldPrice} pts</span>
                    <button class="btn-auction btn-danger-auction btn-sm-auction" onclick="revokeSale(${data.playerId})" title="Revoke Sale" style="padding:0.25rem 0.5rem;font-size:0.7rem">
                        Revoke
                    </button>
                </div>
            </div>
        `;
        soldList.prepend(item);
        document.getElementById('soldCount').textContent = count;
    }

    // Remove from available
    const availItem = document.querySelector(`[data-player-id="${data.playerId}"]`);
    if (availItem) availItem.remove();
    const availCount = document.getElementById('availableCount');
    if (availCount) availCount.textContent = parseInt(availCount.textContent) - 1;

    // Update team display
    updateTeamDisplay(data.teamId, data.teamName);
}

function handlePauseUpdate(isPaused) {
    const pauseBtn = document.getElementById('pauseBtn');
    if (pauseBtn) {
        pauseBtn.innerHTML = isPaused ? 'Resume' : 'Pause';
        pauseBtn.className = isPaused ? 'btn-auction btn-success-auction btn-sm-auction' : 'btn-auction btn-warning-auction btn-sm-auction';
    }
    // Update lobby status badge
    const badge = document.getElementById('lobbyStatusBadge');
    const text = document.getElementById('lobbyStatusText');
    if (badge && text) {
        badge.className = `viewer-status-badge ${isPaused ? 'paused' : 'live'}`;
        text.textContent = isPaused ? 'PAUSED' : 'ACTIVE';
    }
    ToastManager.show(isPaused ? 'Auction paused' : 'Auction resumed', 'info');
}

function handleTeamUpdate(data) {
    const el = document.querySelector(`[data-team-id="${data.teamId}"] .team-points`);
    if (el) el.textContent = `${data.remainingPoints} Points`;
}

function handleAuctionComplete(message) {
    ToastManager.show(message, 'success', 8000);
    // Update lobby status badge
    const badge = document.getElementById('lobbyStatusBadge');
    const text = document.getElementById('lobbyStatusText');
    if (badge && text) {
        badge.className = 'viewer-status-badge ended';
        text.textContent = 'ENDED';
    }
}

function handleSaleRevoked(data) {
    ToastManager.show(`Sale revoked: ${data.playerName} returned from ${data.teamName}`, 'warning', 5000);

    // Remove from sold list
    const soldItem = document.querySelector(`[data-sold-player-id="${data.playerId}"]`);
    if (soldItem) soldItem.remove();
    const soldCountEl = document.getElementById('soldCount');
    if (soldCountEl) soldCountEl.textContent = Math.max(0, parseInt(soldCountEl.textContent) - 1);

    // Add back to available list
    const availList = document.querySelector('.scroll-list .player-card-auction')?.parentElement;
    if (availList) {
        const item = document.createElement('div');
        item.className = 'player-card-auction';
        item.setAttribute('data-player-id', data.playerId);
        item.innerHTML = `
            <div style="display:flex;justify-content:space-between;align-items:center">
                <div>
                    <strong style="font-size:0.9rem">${escapeHtml(data.playerName)}</strong><br>
                    <small style="color:var(--gray-500)"></small>
                </div>
                <button class="btn-auction btn-primary-auction btn-sm-auction" onclick="startAuction(${data.playerId})">
                    Start
                </button>
            </div>
        `;
        availList.prepend(item);
        const availCount = document.getElementById('availableCount');
        if (availCount) availCount.textContent = parseInt(availCount.textContent) + 1;
    }

    // Update team display
    updateTeamDisplay(data.teamId, data.teamName);

    // Update lobby status back to active
    const badge = document.getElementById('lobbyStatusBadge');
    const text = document.getElementById('lobbyStatusText');
    if (badge && text && text.textContent === 'ENDED') {
        badge.className = 'viewer-status-badge live';
        text.textContent = 'ACTIVE';
    }
}

function handleBidReset(data) {
    const bidDisplay = document.getElementById('bidDisplay');
    if (bidDisplay) {
        bidDisplay.innerHTML = `<h3 style="color:var(--gray-400);font-weight:600">Bids reset - Waiting for bids...</h3>`;
    }
    const bidHistoryList = document.getElementById('bidHistoryList');
    if (bidHistoryList) {
        bidHistoryList.innerHTML = '';
    }
    const confirmBtn = document.getElementById('confirmSaleBtn');
    if (confirmBtn) confirmBtn.disabled = true;
    timer.start();
    ToastManager.show(`Bids reset for ${data.playerName}`, 'warning');
}

function handleAuctionReactivated() {
    const badge = document.getElementById('lobbyStatusBadge');
    const text = document.getElementById('lobbyStatusText');
    if (badge && text) {
        badge.className = 'viewer-status-badge live';
        text.textContent = 'ACTIVE';
    }
    ToastManager.show('Auction reactivated!', 'success');
}

function handleTimerUpdate(durationSeconds) {
    timer.setDuration(durationSeconds);
    timer.reset();
    ToastManager.show(`Timer set to ${durationSeconds} seconds`, 'info', 2000);
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
    // Keep only last 10 bids
    while (historyList.children.length > 10) {
        historyList.removeChild(historyList.lastChild);
    }
}

async function updateTeamDisplay(teamId, teamName) {
    try {
        const response = await fetch(`/Auction/AuctionState?lobbyId=${currentLobbyId}`);
        const state = await response.json();
        const team = state.teams?.find(t => t.teamId === teamId);
        if (team) {
            const el = document.querySelector(`[data-team-id="${teamId}"]`);
            if (el) {
                el.querySelector('.team-points').textContent = `${team.remainingPoints} Points`;
                el.querySelector('.team-player-count').textContent = `${team.playerCount} Players`;
            }
        }
    } catch (err) {
        console.error('Failed to update team display:', err);
    }
}

// ─── Actions ───

async function startAuction(playerId) {
    const result = await conn.post('/Auction/StartPlayerAuction', { lobbyId: currentLobbyId, playerId });
    if (!result.success) {
        ToastManager.show(result.message || 'Failed to start auction', 'error');
    }
}

async function confirmSale() {
    if (!currentPlayerId || currentPlayerId === 0) {
        ToastManager.show('No player in auction to confirm', 'warning');
        return;
    }

    const result = await conn.post('/Auction/ConfirmSale', { lobbyId: currentLobbyId, playerId: currentPlayerId });
    if (!result.success) {
        ToastManager.show(result.message || 'Failed to confirm sale', 'error');
    }
}

async function skipPlayer() {
    const result = await conn.post('/Auction/SkipPlayer', { lobbyId: currentLobbyId });
    if (!result.success) {
        ToastManager.show(result.message || 'Failed to skip player', 'error');
    }
}

async function togglePause() {
    const result = await conn.post('/Auction/TogglePause', { lobbyId: currentLobbyId });
    if (!result.success) {
        ToastManager.show(result.message || 'Failed to toggle pause', 'error');
    }
}

async function addPoints(teamId, teamName) {
    const points = prompt(`Add points to ${teamName}:`, '100');
    if (points && !isNaN(points) && parseInt(points) > 0) {
        const result = await conn.post('/Auction/AddPoints', {
            lobbyId: currentLobbyId,
            teamId,
            additionalPoints: parseInt(points)
        });
        if (result.success) {
            ToastManager.show(`Added ${points} points to ${teamName}`, 'success');
        } else {
            ToastManager.show(result.message || 'Failed to add points', 'error');
        }
    }
}

async function deductPoints(teamId, teamName) {
    const points = prompt(`Deduct points from ${teamName}:`, '100');
    if (points && !isNaN(points) && parseInt(points) > 0) {
        const result = await conn.post('/Auction/DeductTeamPoints', {
            lobbyId: currentLobbyId,
            teamId,
            points: parseInt(points)
        });
        if (result.success) {
            ToastManager.show(`Deducted ${points} points from ${teamName}`, 'success');
        } else {
            ToastManager.show(result.message || 'Failed to deduct points', 'error');
        }
    }
}

async function setPoints(teamId, teamName) {
    const points = prompt(`Set ${teamName}'s points to:`, '1000');
    if (points && !isNaN(points) && parseInt(points) >= 0) {
        const result = await conn.post('/Auction/SetTeamPoints', {
            teamId,
            points: parseInt(points)
        });
        if (result.success) {
            ToastManager.show(`Set ${teamName} points to ${points}`, 'success');
        } else {
            ToastManager.show(result.message || 'Failed to set points', 'error');
        }
    }
}

async function revokeSale(playerId) {
    if (!confirm('Are you sure you want to revoke this sale? The player will return to available and points will be refunded.')) return;
    const result = await conn.post('/Auction/RevokeSale', { lobbyId: currentLobbyId, playerId });
    if (result.success) {
        ToastManager.show('Sale revoked successfully', 'success');
    } else {
        ToastManager.show(result.message || 'Failed to revoke sale', 'error');
    }
}

async function resetCurrentBid() {
    if (!confirm('Are you sure you want to reset all bids on the current player?')) return;
    const result = await conn.post('/Auction/ResetCurrentBid', { lobbyId: currentLobbyId });
    if (result.success) {
        ToastManager.show('Bids reset successfully', 'success');
    } else {
        ToastManager.show(result.message || 'Failed to reset bids', 'error');
    }
}

async function endAuction() {
    if (!confirm('Are you sure you want to end the auction? This cannot be undone (but you can reactivate later).')) return;
    const result = await conn.post('/Auction/EndAuction', { lobbyId: currentLobbyId });
    if (result.success) {
        ToastManager.show('Auction ended', 'success');
    } else {
        ToastManager.show(result.message || 'Failed to end auction', 'error');
    }
}

async function reactivateAuction() {
    if (!confirm('Reactivate the auction?')) return;
    const result = await conn.post('/Auction/ReactivateAuction', { lobbyId: currentLobbyId });
    if (result.success) {
        ToastManager.show('Auction reactivated!', 'success');
    } else {
        ToastManager.show(result.message || 'Failed to reactivate auction', 'error');
    }
}

async function setTimerDuration(seconds) {
    const result = await conn.post('/Auction/SetTimerDuration', { lobbyId: currentLobbyId, durationSeconds: seconds });
    if (result.success) {
        ToastManager.show(`Timer set to ${seconds} seconds`, 'success', 2000);
    } else {
        ToastManager.show(result.message || 'Failed to set timer', 'error');
    }
}

async function setBidIncrement(increment) {
    const result = await conn.post('/Auction/SetBidIncrement', { lobbyId: currentLobbyId, bidIncrement: increment });
    if (result.success) {
        ToastManager.show(`Bid increment set to +${increment}`, 'success', 2000);
    } else {
        ToastManager.show(result.message || 'Failed to set bid increment', 'error');
    }
}

async function toggleTeamSuspension(teamId) {
    const result = await conn.post('/Auction/ToggleTeamSuspension', { lobbyId: currentLobbyId, teamId });
    if (result.success) {
        const action = result.data?.isSuspended ? 'Suspended' : 'Unsuspended';
        ToastManager.show(`Team ${action}`, 'success', 2000);
        // Update the button in the team card
        const btn = document.querySelector(`[data-suspend-team-id="${teamId}"]`);
        if (btn) {
            const isSuspended = result.data?.isSuspended;
            btn.textContent = isSuspended ? 'Unsuspend' : 'Suspend';
            btn.className = isSuspended
                ? 'btn-auction btn-success-auction btn-sm-auction'
                : 'btn-auction btn-warning-auction btn-sm-auction';
        }
        // Update team card visual
        const teamCard = document.querySelector(`[data-team-id="${teamId}"]`);
        if (teamCard) {
            if (isSuspended) {
                teamCard.classList.add('team-suspended');
            } else {
                teamCard.classList.remove('team-suspended');
            }
        }
    } else {
        ToastManager.show(result.message || 'Failed to toggle suspension', 'error');
    }
}

function handleTeamSuspension(data) {
    const btn = document.querySelector(`[data-suspend-team-id="${data.teamId}"]`);
    if (btn) {
        btn.textContent = data.isSuspended ? 'Unsuspend' : 'Suspend';
        btn.className = data.isSuspended
            ? 'btn-auction btn-success-auction btn-sm-auction'
            : 'btn-auction btn-warning-auction btn-sm-auction';
    }
    const teamCard = document.querySelector(`[data-team-id="${data.teamId}"]`);
    if (teamCard) {
        if (data.isSuspended) {
            teamCard.classList.add('team-suspended');
        } else {
            teamCard.classList.remove('team-suspended');
        }
    }
}

function handleBidIncrementUpdate(bidIncrement) {
    // Update the active increment button highlight
    document.querySelectorAll('[data-bid-increment]').forEach(btn => {
        btn.style.background = parseInt(btn.dataset.bidIncrement) === bidIncrement
            ? 'rgba(245,158,11,0.2)' : 'rgba(99,102,241,0.1)';
        btn.style.color = parseInt(btn.dataset.bidIncrement) === bidIncrement
            ? '#f59e0b' : '#6366f1';
        btn.style.borderColor = parseInt(btn.dataset.bidIncrement) === bidIncrement
            ? 'rgba(245,158,11,0.5)' : 'rgba(99,102,241,0.3)';
    });
    ToastManager.show(`Bid increment updated to +${bidIncrement}`, 'info', 2000);
}

function exportData() {
    window.open(`/Auction/ExportSummaryCsv/${currentLobbyId}?type=all`, '_blank');
}

async function deletePlayer(playerId) {
    if (!confirm('Are you sure you want to delete this player?')) return;
    const result = await conn.post('/Auction/DeletePlayer', { lobbyId: currentLobbyId, playerId });
    if (result.success) {
        const el = document.querySelector(`[data-player-id="${playerId}"]`);
        if (el) el.remove();
        ToastManager.show('Player deleted', 'info');
    } else {
        ToastManager.show(result.message || 'Failed to delete player', 'error');
    }
}

async function fetchAuctionState() {
    try {
        const response = await fetch(`/Auction/AuctionState?lobbyId=${currentLobbyId}`);
        const state = await response.json();
        if (state.currentPlayer) {
            currentPlayerId = state.currentPlayer.playerId;
            handlePlayerUpdate(state.currentPlayer);
        }
    } catch (err) {
        console.error('Failed to fetch auction state:', err);
    }
}

function onTimerExpired() {
    // If there's a highest bidder, auto-confirm the sale
    const bidDisplay = document.getElementById('bidDisplay');
    const hasBid = bidDisplay && bidDisplay.querySelector('.bid-amount');

    if (hasBid && currentPlayerId > 0) {
        ToastManager.show('Time expired — auto-confirming sale!', 'warning');
        confirmSale();
    } else {
        ToastManager.show('Bidding time expired — skipping player', 'warning');
        skipPlayer();
    }
}
