class MouseSpeedTracker {
    constructor() {
        this.movements = [];
        this.lastPosition = null;
        this.lastTime = null;
        this.lastSpeed = 0;
        this.isTracking = false;
        this.handler = null;
    }

    startTracking() {
        this.isTracking = true;
        this.handler = (e) => this.recordMovement(e);
        document.addEventListener('mousemove', this.handler);
    }

    recordMovement(e) {
        const now = Date.now();
        const current = { x: e.clientX, y: e.clientY, time: now };

        if (this.lastPosition) {
            const dx = current.x - this.lastPosition.x;
            const dy = current.y - this.lastPosition.y;
            const dt = (now - this.lastTime) / 1000;
            if (dt > 0) {
                const distance = Math.sqrt(dx * dx + dy * dy);
                const speed = distance / dt;
                const acceleration = (speed - this.lastSpeed) / dt;
                this.lastSpeed = speed;

                this.movements.push({
                    timestamp: new Date(now).toISOString(),
                    x: Math.round(current.x),
                    y: Math.round(current.y),
                    speed: parseFloat(speed.toFixed(2)),
                    acceleration: parseFloat(acceleration.toFixed(2))
                });

                // Keep max 500 samples
                if (this.movements.length > 500) this.movements.shift();
            }
        }

        this.lastPosition = current;
        this.lastTime = now;
    }

    stopTracking() {
        this.isTracking = false;
        if (this.handler) {
            document.removeEventListener('mousemove', this.handler);
        }
    }

    getData() {
        const speeds = this.movements.map(m => m.speed);
        const avgSpeed = speeds.length > 0
            ? speeds.reduce((a, b) => a + b, 0) / speeds.length
            : 0;

        return {
            movements: this.movements,
            averageSpeed: parseFloat(avgSpeed.toFixed(2)),
            maxSpeed: speeds.length > 0 ? Math.max(...speeds) : 0,
            totalSamples: this.movements.length
        };
    }
}