class HeadTracker {
    static sharedModel = null;
    static sharedModelPromise = null;

    constructor() {
        this.headPositions = [];
        this.headSamples = [];
        this.isTracking = false;
        this.stream = null;
        this.video = null;
        this.model = null;
        this.sampleTimer = null;
        this.lastPosition = null;
        this.lookAwayCount = 0;
        this.tiltCount = 0;
        this.movementSum = 0;
        this.movementCount = 0;
        this.sampleIntervalMs = 333; // ~3 FPS for more sensitive look-away detection
        this.isInferenceRunning = false;
        this.wasLookAwayLastSample = false;

        // Treat face near frame edges (or too small) as look-away even if still detected.
        this.lookAwayThresholds = {
            left: 0.24,
            right: 0.76,
            top: 0.24,
            bottom: 0.76,
            minFaceAreaRatio: 0.03
        };
    }

    async startTracking() {
        try {
            if (this.isTracking) {
                return true;
            }

            this.headPositions = [];
            this.headSamples = [];
            this.lastPosition = null;
            this.lookAwayCount = 0;
            this.tiltCount = 0;
            this.movementSum = 0;
            this.movementCount = 0;
            this.wasLookAwayLastSample = false;

            // Low-res camera stream to reduce CPU/GPU load.
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    width: { ideal: 160, max: 320 },
                    height: { ideal: 120, max: 240 },
                    frameRate: { ideal: 6, max: 8 }
                }
            });

            this.video = document.createElement('video');
            this.video.srcObject = this.stream;
            this.video.style.display = 'none';
            await this.video.play();

            // Load BlazeFace once and reuse across sections/restarts.
            if (typeof tf !== 'undefined' && typeof blazeface !== 'undefined') {
                if (HeadTracker.sharedModel) {
                    this.model = HeadTracker.sharedModel;
                } else {
                    if (!HeadTracker.sharedModelPromise) {
                        HeadTracker.sharedModelPromise = blazeface.load();
                    }
                    this.model = await HeadTracker.sharedModelPromise;
                    HeadTracker.sharedModel = this.model;
                }

                this.isTracking = true;
                // Capture one sample immediately so short sessions still record data.
                await this.trackFace();
                this.sampleTimer = setInterval(() => this.trackFace(), this.sampleIntervalMs);
                console.log('✅ Head tracking started');
                return true;
            } else {
                console.warn('⚠️ TensorFlow/BlazeFace not loaded - head tracking unavailable');
                this.isTracking = false;
                return false;
            }
        } catch (err) {
            console.warn('⚠️ Camera access denied or unavailable:', err.message);
            this.isTracking = false;
            return false;
        }
    }

    async trackFace() {
        if (!this.isTracking || !this.model || !this.video) return;
        if (this.isInferenceRunning) {
            return;
        }
        this.isInferenceRunning = true;

        try {
            const predictions = await this.model.estimateFaces(this.video, false);

            if (predictions.length > 0) {
                const nowIso = new Date().toISOString();
                const face = predictions[0];
                const topLeft = face.topLeft;
                const bottomRight = face.bottomRight;

                const x = (topLeft[0] + bottomRight[0]) / 2;
                const y = (topLeft[1] + bottomRight[1]) / 2;

                const frameWidth = this.video.videoWidth || 160;
                const frameHeight = this.video.videoHeight || 120;
                const normalizedX = x / frameWidth;
                const normalizedY = y / frameHeight;
                const faceWidthPx = Math.max(1, bottomRight[0] - topLeft[0]);
                const faceHeightPx = Math.max(1, bottomRight[1] - topLeft[1]);
                const faceAreaRatio = (faceWidthPx * faceHeightPx) / (frameWidth * frameHeight);

                const isLookAway =
                    normalizedX < this.lookAwayThresholds.left ||
                    normalizedX > this.lookAwayThresholds.right ||
                    normalizedY < this.lookAwayThresholds.top ||
                    normalizedY > this.lookAwayThresholds.bottom ||
                    faceAreaRatio < this.lookAwayThresholds.minFaceAreaRatio;

                if (isLookAway && !this.wasLookAwayLastSample) {
                    this.lookAwayCount++;
                }
                this.wasLookAwayLastSample = isLookAway;

                // Estimate tilt using landmarks
                const landmarks = face.landmarks;
                const leftEye = landmarks[0];
                const rightEye = landmarks[1];

                // Z approximation: face size (larger = closer)
                const faceWidth = bottomRight[0] - topLeft[0];
                const z = 1000 / faceWidth; // inverse of size = depth estimate

                // Tilt angle in degrees
                const dx = rightEye[0] - leftEye[0];
                const dy = rightEye[1] - leftEye[1];
                const tiltAngle = Math.atan2(dy, dx) * (180 / Math.PI);

                // Movement delta from last position
                let movementDelta = 0;
                if (this.lastPosition) {
                    const ddx = x - this.lastPosition.x;
                    const ddy = y - this.lastPosition.y;
                    movementDelta = parseFloat(Math.sqrt(ddx * ddx + ddy * ddy).toFixed(2));
                }

                // Detect tilt (head tilted more than 15 degrees)
                const isHeadTilt = Math.abs(tiltAngle) > 15;
                if (isHeadTilt) this.tiltCount++;

                if (movementDelta > 0) {
                    this.movementSum += movementDelta;
                    this.movementCount++;
                }
                this.lastPosition = { x, y };

                this.headPositions.push({
                    timestamp: nowIso,
                    x: parseFloat(x.toFixed(2)),
                    y: parseFloat(y.toFixed(2)),
                    z: parseFloat(z.toFixed(2)),
                    movementDelta: movementDelta
                });

                this.headSamples.push({
                    timestamp: nowIso,
                    x: parseFloat(x.toFixed(2)),
                    y: parseFloat(y.toFixed(2)),
                    z: parseFloat(z.toFixed(2)),
                    movementDelta: movementDelta,
                    isLookAway: isLookAway,
                    isHeadTilt: isHeadTilt
                });

                if (this.headPositions.length > 500) this.headPositions.shift();
                if (this.headSamples.length > 700) this.headSamples.shift();

            } else {
                // No face detected = looking away
                if (!this.wasLookAwayLastSample) {
                    this.lookAwayCount++;
                }
                this.wasLookAwayLastSample = true;

                this.headSamples.push({
                    timestamp: new Date().toISOString(),
                    x: 0,
                    y: 0,
                    z: 0,
                    movementDelta: 0,
                    isLookAway: true,
                    isHeadTilt: false
                });

                if (this.headSamples.length > 700) this.headSamples.shift();
            }
        } catch (e) {
            // Silently continue
        } finally {
            this.isInferenceRunning = false;
        }
    }

    stopTracking() {
        this.isTracking = false;
        if (this.sampleTimer) {
            clearInterval(this.sampleTimer);
            this.sampleTimer = null;
        }
        if (this.stream) {
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
        }
        if (this.video) {
            this.video.srcObject = null;
            this.video = null;
        }
    }

    getData() {
        const avgMovement = this.movementCount > 0
            ? parseFloat((this.movementSum / this.movementCount).toFixed(2))
            : 0;

        let derivedLookAwayCount = 0;
        let lastLookAwayState = false;
        for (const sample of this.headSamples) {
            if (sample.isLookAway && !lastLookAwayState) {
                derivedLookAwayCount++;
            }
            lastLookAwayState = sample.isLookAway;
        }
        const derivedTiltCount = this.headSamples.filter(sample => sample.isHeadTilt).length;

        return {
            headPositions: this.headPositions,
            headSamples: this.headSamples,
            averageHeadMovement: avgMovement,
            lookAwayCount: Math.max(this.lookAwayCount, derivedLookAwayCount),
            tiltCount: Math.max(this.tiltCount, derivedTiltCount)
        };
    }
}