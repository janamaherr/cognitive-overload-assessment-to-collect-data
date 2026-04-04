class BehaviorLogger {
    constructor(sessionId, sectionNumber) {
        this.sessionId = sessionId;
        this.sectionNumber = sectionNumber;
        this.typingEvents = [];
        this.hesitationPauses = [];
        this.lastKeyTime = null;
        this.pauseThreshold = 2000; // 2 seconds = hesitation
        this.currentPause = null;
        this.gameStartTime = null;
        this.keyHandler = null;
        this.pauseTimer = null;
    }

    startGame(gameType) {
        this.gameType = gameType;
        this.gameStartTime = Date.now();
        this.typingEvents = [];
        this.hesitationPauses = [];
        this.lastKeyTime = null;

        // Typing speed tracking
        this.keyHandler = (e) => this.recordKeyPress(e);
        document.addEventListener('keydown', this.keyHandler);

        // Start hesitation pause timer
        this.resetPauseTimer();

        console.log('BehaviorLogger started for:', gameType);
    }

    recordKeyPress(e) {
        const now = Date.now();
        const delay = this.lastKeyTime ? now - this.lastKeyTime : 0;
        const speed = delay > 0 ? 1000 / delay : 0; // chars per second

        this.typingEvents.push({
            timestamp: new Date(now).toISOString(),
            key: e.key,
            delaySinceLastKeyMs: Math.round(delay),
            speed: parseFloat(speed.toFixed(2))
        });

        // End any active hesitation pause on keypress
        if (this.currentPause) {
            this.currentPause.endTime = new Date(now).toISOString();
            this.currentPause.durationSeconds = parseFloat(((now - new Date(this.currentPause.startTime).getTime()) / 1000).toFixed(2));
            this.hesitationPauses.push({ ...this.currentPause });
            this.currentPause = null;
        }

        this.lastKeyTime = now;
        this.resetPauseTimer();
    }

    resetPauseTimer() {
        if (this.pauseTimer) clearTimeout(this.pauseTimer);
        this.pauseTimer = setTimeout(() => {
            // User has been idle for pauseThreshold ms - start a hesitation pause
            this.currentPause = {
                startTime: new Date().toISOString(),
                endTime: null,
                durationSeconds: 0,
                location: `section${this.sectionNumber}`
            };
        }, this.pauseThreshold);
    }

    logMouseClick(targetIndex) {
        // Close any hesitation pause on click
        if (this.currentPause) {
            const now = Date.now();
            this.currentPause.endTime = new Date(now).toISOString();
            this.currentPause.durationSeconds = parseFloat(((now - new Date(this.currentPause.startTime).getTime()) / 1000).toFixed(2));
            this.hesitationPauses.push({ ...this.currentPause });
            this.currentPause = null;
        }
        this.resetPauseTimer();
    }

    endGame() {
        // Clean up
        if (this.keyHandler) document.removeEventListener('keydown', this.keyHandler);
        if (this.pauseTimer) clearTimeout(this.pauseTimer);

        // Close any open pause
        if (this.currentPause) {
            const now = Date.now();
            this.currentPause.endTime = new Date(now).toISOString();
            this.currentPause.durationSeconds = parseFloat(((now - new Date(this.currentPause.startTime).getTime()) / 1000).toFixed(2));
            this.hesitationPauses.push({ ...this.currentPause });
            this.currentPause = null;
        }

        const avgTypingSpeed = this.typingEvents.length > 1
            ? parseFloat((this.typingEvents.reduce((s, e) => s + e.speed, 0) / this.typingEvents.length).toFixed(2))
            : 0;

        return {
            typingEvents: this.typingEvents,
            hesitationPauses: this.hesitationPauses.filter(p => p.endTime != null),
            hesitationPauseCount: this.hesitationPauses.filter(p => p.endTime != null).length,
            averageTypingSpeed: avgTypingSpeed
        };
    }
}