// AuctionArena - SignalR Connection Manager
// Handles connection, reconnection, and state synchronization

class AuctionConnection {
    constructor(hubUrl) {
        this.hubUrl = hubUrl;
        this.connection = null;
        this.lobbyId = null;
        this.handlers = {};
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
    }

    async connect(lobbyId) {
        this.lobbyId = lobbyId;
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.hubUrl)
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.setupEventHandlers();
        this.setupReconnectionHandlers();

        try {
            await this.connection.start();
            await this.connection.invoke('JoinLobby', this.lobbyId);
            this.updateConnectionStatus('connected');
            console.log('Connected to auction hub');
        } catch (err) {
            console.error('Connection error:', err);
            this.updateConnectionStatus('disconnected');
        }
    }

    setupEventHandlers() {
        this.connection.on('ReceiveBidUpdate', (data) => {
            this.trigger('bidUpdate', data);
        });

        this.connection.on('ReceivePlayerUpdate', (data) => {
            this.trigger('playerUpdate', data);
        });

        this.connection.on('ReceivePlayerSold', (data) => {
            this.trigger('playerSold', data);
        });

        this.connection.on('ReceivePauseUpdate', (isPaused) => {
            this.trigger('pauseUpdate', isPaused);
        });

        this.connection.on('ReceiveTeamUpdate', (data) => {
            this.trigger('teamUpdate', data);
        });

        this.connection.on('ReceiveAuctionComplete', (message) => {
            this.trigger('auctionComplete', message);
        });
    }

    setupReconnectionHandlers() {
        this.connection.onreconnecting(() => {
            this.updateConnectionStatus('reconnecting');
            this.reconnectAttempts++;
            console.log(`Reconnecting... attempt ${this.reconnectAttempts}`);
        });

        this.connection.onreconnected(async () => {
            this.updateConnectionStatus('connected');
            this.reconnectAttempts = 0;
            if (this.lobbyId) {
                await this.connection.invoke('JoinLobby', this.lobbyId);
                this.trigger('reconnected', null);
            }
            console.log('Reconnected to auction hub');
        });

        this.connection.onclose(() => {
            this.updateConnectionStatus('disconnected');
            console.log('Connection closed');
        });
    }

    on(event, handler) {
        if (!this.handlers[event]) this.handlers[event] = [];
        this.handlers[event].push(handler);
    }

    trigger(event, data) {
        if (this.handlers[event]) {
            this.handlers[event].forEach(h => h(data));
        }
    }

    updateConnectionStatus(status) {
        const el = document.getElementById('connectionStatus');
        if (el) {
            el.className = `connection-status ${status}`;
            const labels = { connected: 'Connected', disconnected: 'Disconnected', reconnecting: 'Reconnecting...' };
            el.innerHTML = `<span style="width:8px;height:8px;border-radius:50%;background:currentColor"></span> ${labels[status] || status}`;
        }
    }

    // Helper: get anti-forgery token
    getAntiForgeryToken() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    // Helper: POST with CSRF token
    async post(url, body) {
        const token = this.getAntiForgeryToken();
        const formData = new URLSearchParams();
        formData.append('__RequestVerificationToken', token);
        for (const [key, value] of Object.entries(body)) {
            formData.append(key, value);
        }

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: formData.toString()
            });

            const text = await response.text();
            try {
                return JSON.parse(text);
            } catch {
                return { success: false, message: `Server error (${response.status}). Please try again.` };
            }
        } catch (err) {
            console.error('POST error:', err);
            return { success: false, message: 'Network error. Please try again.' };
        }
    }
}

// Toast notification system
class ToastManager {
    static show(message, type = 'info', duration = 4000) {
        let container = document.querySelector('.toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container';
            document.body.appendChild(container);
        }

        const icons = {
            success: '<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"/></svg>',
            error: '<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"/></svg>',
            info: '<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"/></svg>',
            warning: '<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"/></svg>'
        };

        const toast = document.createElement('div');
        toast.className = `toast-auction toast-${type}`;
        toast.innerHTML = `${icons[type] || ''} ${this.escapeHtml(message)}`;
        container.appendChild(toast);

        setTimeout(() => {
            toast.style.animation = 'fadeOut 0.3s ease forwards';
            setTimeout(() => toast.remove(), 300);
        }, duration);
    }

    static escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Countdown Timer
class CountdownTimer {
    constructor(onExpire, durationSeconds = 30) {
        this.duration = durationSeconds;
        this.remaining = 0;
        this.onExpire = onExpire;
        this.interval = null;
        this.isRunning = false;
    }

    start(resetOnBid = true) {
        this.remaining = this.duration;
        this.isRunning = true;
        this.clearInterval();
        this.interval = setInterval(() => {
            this.remaining--;
            this.updateDisplay();
            if (this.remaining <= 0) {
                this.stop();
                if (this.onExpire) this.onExpire();
            }
        }, 1000);
        this.updateDisplay();
    }

    reset() {
        if (this.isRunning) {
            this.remaining = this.duration;
            this.updateDisplay();
        }
    }

    stop() {
        this.isRunning = false;
        this.clearInterval();
        this.remaining = 0;
        this.updateDisplay();
    }

    clearInterval() {
        if (this.interval) {
            clearInterval(this.interval);
            this.interval = null;
        }
    }

    updateDisplay() {
        const el = document.getElementById('countdownTimer');
        if (!el) return;

        if (this.remaining <= 0) {
            el.textContent = '';
            el.classList.remove('urgent');
            return;
        }

        const mins = Math.floor(this.remaining / 60);
        const secs = this.remaining % 60;
        el.textContent = `${mins}:${secs.toString().padStart(2, '0')}`;

        if (this.remaining <= 10) {
            el.classList.add('urgent');
        } else {
            el.classList.remove('urgent');
        }
    }
}

// Utility: Escape HTML to prevent XSS
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}