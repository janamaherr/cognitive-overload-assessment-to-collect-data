class HeadPoseTracker {
    constructor(sessionId, sectionNumber) {
        this.sessionId = sessionId;
        this.sectionNumber = sectionNumber;
        this.faceLandmarker = null;
        this.video = null;
        this.canvas = null;
        this.ctx = null;
        this.isTracking = false;
        this.headPositions = [];
        this.lookingAwayEvents = [];
        this.headTiltEvents = [];
        this.lastLogTime = Date.now();
        this.currentAngles = { yaw: 0, pitch: 0, roll: 0 };
        
        // Thresholds for detection
        this.thresholds = {
            lookingAway: 20, // degrees - looking left/right
            lookingDown: 15,  // degrees - looking down
            headTilt: 15,     // degrees - head tilt
            samplingRate: 100 // ms between samples
        };
        
        // Camera matrix for PnP calculation (approximate for webcam)
        this.cameraMatrix = [
            [640, 0, 320],
            [0, 480, 240],
            [0, 0, 1]
        ];
        
        // 3D model points of an average face (in mm)
        this.modelPoints = {
            noseTip: [0.0, 0.0, 0.0],
            chin: [0.0, -330.0, -65.0],
            leftEyeLeftCorner: [-225.0, 170.0, -135.0],
            leftEyeRightCorner: [-125.0, 170.0, -135.0],
            rightEyeLeftCorner: [125.0, 170.0, -135.0],
            rightEyeRightCorner: [225.0, 170.0, -135.0],
            leftMouthCorner: [-150.0, -150.0, -125.0],
            rightMouthCorner: [150.0, -150.0, -125.0]
        };
        
        this.init();
    }
    
    async init() {
        try {
            // Load MediaPipe
            const { FaceLandmarker, FilesetResolver } = await import('https://cdn.skypack.dev/@mediapipe/tasks-vision');
            
            const vision = await FilesetResolver.forVisionTasks('https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.0/wasm');
            
            this.faceLandmarker = await FaceLandmarker.createFromOptions(vision, {
                baseOptions: {
                    modelAssetPath: 'https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task',
                    delegate: 'CPU'
                },
                outputFaceBlendshapes: false,
                runningMode: 'VIDEO',
                numFaces: 1,
                minFaceDetectionConfidence: 0.5,
                minFacePresenceConfidence: 0.5,
                minTrackingConfidence: 0.5
            });
            
            console.log('HeadPoseTracker initialized successfully');
        } catch (error) {
            console.error('Failed to initialize HeadPoseTracker:', error);
        }
    }
    
    async startTracking() {
        if (!this.faceLandmarker) {
            console.log('Waiting for MediaPipe to initialize...');
            setTimeout(() => this.startTracking(), 1000);
            return;
        }
        
        try {
            // Create hidden video element
            this.video = document.createElement('video');
            this.video.width = 640;
            this.video.height = 480;
            this.video.style.display = 'none';
            this.video.autoplay = true;
            this.video.playsInline = true;
            document.body.appendChild(this.video);
            
            // Create hidden canvas for processing
            this.canvas = document.createElement('canvas');
            this.canvas.width = 640;
            this.canvas.height = 480;
            this.canvas.style.display = 'none';
            document.body.appendChild(this.canvas);
            this.ctx = this.canvas.getContext('2d');
            
            // Get camera access
            const stream = await navigator.mediaDevices.getUserMedia({ 
                video: { 
                    width: 640, 
                    height: 480,
                    facingMode: 'user'
                } 
            });
            
            this.video.srcObject = stream;
            
            await new Promise((resolve) => {
                this.video.onloadeddata = () => {
                    this.video.play();
                    resolve();
                };
            });
            
            this.isTracking = true;
            this.track();
            
            console.log('Head tracking started');
        } catch (error) {
            console.error('Failed to start head tracking:', error);
        }
    }
    
    stopTracking() {
        this.isTracking = false;
        
        // Stop video stream
        if (this.video && this.video.srcObject) {
            const tracks = this.video.srcObject.getTracks();
            tracks.forEach(track => track.stop());
        }
        
        // Remove elements
        if (this.video) this.video.remove();
        if (this.canvas) this.canvas.remove();
        
        console.log('Head tracking stopped');
    }
    
    async track() {
        if (!this.isTracking) return;
        
        try {
            const startTimeMs = performance.now();
            const detections = await this.faceLandmarker.detectForVideo(this.video, startTimeMs);
            
            if (detections.faceLandmarks.length > 0) {
                const landmarks = detections.faceLandmarks[0];
                
                // Calculate head pose angles
                const angles = this.calculateHeadAngles(landmarks);
                this.currentAngles = angles;
                
                // Log position periodically
                const now = Date.now();
                if (now - this.lastLogTime >= this.thresholds.samplingRate) {
                    this.logHeadPosition(angles);
                    this.detectLookingAway(angles);
                    this.detectHeadTilt(angles);
                    this.lastLogTime = now;
                }
                
                // Optional: Draw debug visualization
                this.drawDebugInfo(landmarks);
            }
        } catch (error) {
            console.error('Error in head tracking:', error);
        }
        
        requestAnimationFrame(() => this.track());
    }
    
    calculateHeadAngles(landmarks) {
        // Map MediaPipe landmarks to our model points
        const imagePoints = {
            noseTip: this.getLandmark2D(landmarks, 1), // Nose tip
            chin: this.getLandmark2D(landmarks, 152), // Chin
            leftEyeLeftCorner: this.getLandmark2D(landmarks, 33), // Left eye left corner
            leftEyeRightCorner: this.getLandmark2D(landmarks, 133), // Left eye right corner
            rightEyeLeftCorner: this.getLandmark2D(landmarks, 362), // Right eye left corner
            rightEyeRightCorner: this.getLandmark2D(landmarks, 263), // Right eye right corner
            leftMouthCorner: this.getLandmark2D(landmarks, 61), // Left mouth corner
            rightMouthCorner: this.getLandmark2D(landmarks, 291) // Right mouth corner
        };
        
        // Simplified pose estimation using average of key points
        // Note: For production, you'd want to implement proper solvePnP
        const nosePoint = imagePoints.noseTip;
        const leftEye = this.averagePoints([imagePoints.leftEyeLeftCorner, imagePoints.leftEyeRightCorner]);
        const rightEye = this.averagePoints([imagePoints.rightEyeLeftCorner, imagePoints.rightEyeRightCorner]);
        
        // Calculate yaw (left-right) based on nose position relative to eyes
        const eyeCenterX = (leftEye.x + rightEye.x) / 2;
        const yaw = (nosePoint.x - eyeCenterX) * 0.15; // Scale factor
        
        // Calculate pitch (up-down) based on nose vertical position
        const eyeCenterY = (leftEye.y + rightEye.y) / 2;
        const pitch = (eyeCenterY - nosePoint.y) * 0.15;
        
        // Calculate roll (tilt) based on eye line angle
        const eyeDeltaX = rightEye.x - leftEye.x;
        const eyeDeltaY = rightEye.y - leftEye.y;
        const roll = Math.atan2(eyeDeltaY, eyeDeltaX) * (180 / Math.PI);
        
        return {
            yaw: Math.max(-45, Math.min(45, yaw)),
            pitch: Math.max(-30, Math.min(30, pitch)),
            roll: roll
        };
    }
    
    getLandmark2D(landmarks, index) {
        return {
            x: landmarks[index].x * this.canvas.width,
            y: landmarks[index].y * this.canvas.height,
            z: landmarks[index].z || 0
        };
    }
    
    averagePoints(points) {
        return {
            x: points.reduce((sum, p) => sum + p.x, 0) / points.length,
            y: points.reduce((sum, p) => sum + p.y, 0) / points.length
        };
    }
    
    logHeadPosition(angles) {
        this.headPositions.push({
            timestamp: new Date().toISOString(),
            yaw: angles.yaw,
            pitch: angles.pitch,
            roll: angles.roll
        });
        
        // Keep only last 1000 positions to avoid memory issues
        if (this.headPositions.length > 1000) {
            this.headPositions = this.headPositions.slice(-1000);
        }
    }
    
    detectLookingAway(angles) {
        if (Math.abs(angles.yaw) > this.thresholds.lookingAway || 
            Math.abs(angles.pitch) > this.thresholds.lookingDown) {
            
            this.lookingAwayEvents.push({
                timestamp: new Date().toISOString(),
                type: Math.abs(angles.yaw) > this.thresholds.lookingAway ? 'left-right' : 'up-down',
                angles: { ...angles }
            });
            
            // Keep only last 100 events
            if (this.lookingAwayEvents.length > 100) {
                this.lookingAwayEvents = this.lookingAwayEvents.slice(-100);
            }
            
            console.log('Looking away detected:', angles);
        }
    }
    
    detectHeadTilt(angles) {
        if (Math.abs(angles.roll) > this.thresholds.headTilt) {
            this.headTiltEvents.push({
                timestamp: new Date().toISOString(),
                angle: angles.roll,
                direction: angles.roll > 0 ? 'right' : 'left'
            });
            
            // Keep only last 100 events
            if (this.headTiltEvents.length > 100) {
                this.headTiltEvents = this.headTiltEvents.slice(-100);
            }
            
            console.log('Head tilt detected:', angles.roll);
        }
    }
    
    drawDebugInfo(landmarks) {
        // Optional: Draw face mesh on canvas for debugging
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        this.ctx.drawImage(this.video, 0, 0, this.canvas.width, this.canvas.height);
        
        // Draw angles
        this.ctx.font = '16px Arial';
        this.ctx.fillStyle = 'red';
        this.ctx.fillText(`Yaw: ${this.currentAngles.yaw.toFixed(1)}°`, 10, 30);
        this.ctx.fillText(`Pitch: ${this.currentAngles.pitch.toFixed(1)}°`, 10, 60);
        this.ctx.fillText(`Roll: ${this.currentAngles.roll.toFixed(1)}°`, 10, 90);
        
        // Draw status
        if (Math.abs(this.currentAngles.yaw) > this.thresholds.lookingAway) {
            this.ctx.fillStyle = 'orange';
            this.ctx.fillText('⚠️ LOOKING AWAY', 10, 120);
        }
        if (Math.abs(this.currentAngles.roll) > this.thresholds.headTilt) {
            this.ctx.fillStyle = 'orange';
            this.ctx.fillText('⚠️ HEAD TILTED', 10, 150);
        }
    }
    
    getData() {
        return {
            headPositions: this.headPositions,
            lookingAwayEvents: this.lookingAwayEvents,
            headTiltEvents: this.headTiltEvents,
            averageYaw: this.headPositions.reduce((sum, p) => sum + p.yaw, 0) / (this.headPositions.length || 1),
            averagePitch: this.headPositions.reduce((sum, p) => sum + p.pitch, 0) / (this.headPositions.length || 1),
            averageRoll: this.headPositions.reduce((sum, p) => sum + p.roll, 0) / (this.headPositions.length || 1),
            maxYaw: Math.max(...this.headPositions.map(p => Math.abs(p.yaw))),
            maxPitch: Math.max(...this.headPositions.map(p => Math.abs(p.pitch))),
            maxRoll: Math.max(...this.headPositions.map(p => Math.abs(p.roll))),
            lookingAwayCount: this.lookingAwayEvents.length,
            headTiltCount: this.headTiltEvents.length
        };
    }
    
    clearData() {
        this.headPositions = [];
        this.lookingAwayEvents = [];
        this.headTiltEvents = [];
    }
}

export default HeadPoseTracker;