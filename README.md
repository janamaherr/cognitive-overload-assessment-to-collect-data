# Cognitive Overload LMS — Data Collection System

A web-based platform designed to measure cognitive overload through interactive games while passively collecting behavioral data. Players complete three game challenges, and the system records mouse movement, typing speed, hesitation patterns, and head movement in real time to support cognitive load research.

---

## Purpose

This system is built for research purposes. By having participants play short cognitive games, the platform collects behavioral signals that may indicate levels of cognitive overload — without interrupting the player experience. All data is stored in MongoDB for later analysis.

---

## Games

### Section 1 — Memory Pattern Challenge
Players watch a sequence of highlighted squares on a grid, then must repeat the pattern by clicking the squares in the same order.
- Fixed sequence length of 5 steps
- Timer starts only when the player's turn begins (after the sequence display ends)
- Leaderboard: ranked by fastest completion time (winners only)

### Section 2 — Sentence Memory Test
Players view sentences of increasing length (5–9 words) that appear briefly, then disappear. Players must type them from memory.
- Sentences progress from 5 words → 9 words across 5 rounds
- Each sentence is visible for 3–6 seconds depending on length (1 extra second per word beyond 5)
- Correct sentences award 100 points each
- **Timing metrics**: per-word time-to-first-keystroke and submission time are recorded for cognitive load analysis
- Leaderboard: ranked by score then number of correct sentences

### Section 3 — Twisty Arrow Game
Players shoot arrows into a rotating circle without hitting any already-stuck arrows.
- Click or press **Space** to shoot
- Each successful arrow increases the score
- Hitting an existing arrow ends the game
- Leaderboard: ranked by score, then by fastest time for ties

---

## Behavioral Data Collected

For every game session the following signals are recorded and stored:

| Signal | Description |
|---|---|
| Average Mouse Speed | Mean speed of mouse movements during gameplay |
| Average Typing Speed | Mean characters-per-second while typing answers |
| Hesitation Pauses | Number of input pauses above a threshold (capped at 30) |
| Average Head Movement | Mean displacement of detected head position between frames |
| Look-Away Count | Number of times the player looked away from the screen |
| Head Tilt Count | Number of significant head tilts detected |

Head tracking uses **TensorFlow.js** and **BlazeFace** running entirely in the browser via the device webcam. Inference runs at 1 frame per second to minimise performance impact.

> Only aggregate metrics are stored — no raw position arrays or video data are saved.

---

## ML Integration

Each finished game also runs through a machine learning prediction flow so the session can be analyzed with both the raw telemetry and the model output.

### Prediction Flow

1. The game view saves the completed result to `/api/GameData/save`.
2. The browser then posts a compact feature payload to `/api/GameData/ml-predict`.
3. `GameDataController` forwards the same JSON to the Python ML service at `http://localhost:8000/predict`.
4. The backend reads the ML response and extracts:
   - `prediction` - binary class output
   - `probability` - model confidence
   - `label` - human-readable class label
   - `breakdown` - component scores for behavioral, physiological, and contextual signals
5. The prediction is written back into MongoDB in three places:
   - `GameResults` / `GameResults_Section1` / `GameResults_Section2` / `GameResults_Section3` via `mlPrediction`
   - `UserSessions.Games[].mlPrediction`
   - `MLPredictions` as a full request/response log

### ML Request Payload

The frontend sends a flattened JSON payload with the features the model expects:

- `gameId`
- `sessionId`
- `mouseMovementsCount`
- `averageMouseSpeed`
- `typingEventsCount`
- `averageTypingSpeed`
- `hesitationPausesCount`
- `averageHeadMovement`
- `lookAwayCount`
- `headTiltCount`
- `totalTimeSeconds`
- `heartRate`
- `heartRatebefore`
- `heartRateDifference`
- `age`
- `gameType`
- `sectionNumber`
- `score`
- `completed`

### ML Response Shape

The backend expects the Python service to return JSON like this:

```json
{
  "prediction": 1,
  "probability": 0.84,
  "label": "Overloaded",
  "breakdown": {
    "behavioral": 0.62,
    "physiological": 0.71,
    "contextual": 0.43
  }
}
```

### Stored ML Data

The prediction log collection stores the original request, the raw response, and the parsed model fields for auditing and debugging.

| Field | Stored In |
|---|---|
| Prediction, probability, label, breakdown | `GameResult.MLPrediction` and `UserSession.Games[].MLPrediction` |
| Full request payload | `MLPredictions.requestPayload` |
| Full response payload | `MLPredictions.responsePayload` |
| Session, section, game type, score, completion flag | `MLPredictions` |

### Notes

- Heart-rate fields are nullable so games can be saved even when no wearable signal is available.
- The on-screen ML result box is hidden from players; predictions are stored for analysis instead of being shown during play.
- The frontend includes timeout handling so a slow ML service does not block the game flow.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 9 MVC (C#) |
| Frontend | Razor Views, Bootstrap 5, vanilla JavaScript |
| Database | MongoDB Atlas |
| Head Tracking | TensorFlow.js 3.21.0 + BlazeFace 0.0.7 |
| Behavior Logging | Custom `BehaviorLogger`, `MouseSpeedTracker`, `HeadTracker` JS classes |
| ML Inference | Python service at `http://localhost:8000/predict` |

---

## Project Structure

```
Controllers/
  GameDataController.cs     — API endpoints for saving results, surveys, leaderboards, and ML predictions
  HomeController.cs
  QuestionController.cs

Models/
  GameResult.cs             — Top-level result document saved to MongoDB
  GameSpecificData.cs       — Per-section game metadata (sparse/nullable fields)
  BehaviorData.cs           — Behavioral signal container
  MLPredictionLog.cs        — Audit log for every ML request/response
  UserSession.cs

Services/
  MongoDBService.cs         — MongoDB connection and collection access

Views/Questions/
  Section1.cshtml           — Memory Pattern Challenge
  Section2.cshtml           — Sentence Memory Test
  Section3.cshtml           — Twisty Arrow Game

wwwroot/js/
  behaviorLogger.js         — Typing speed & hesitation tracking
  mouseSpeed.js             — Mouse movement speed tracking
  headTracker.js            — Webcam-based head movement tracking
```

---

## MongoDB Collections

| Collection | Contents |
|---|---|
| `GameResults` | All game results across all sections |
| `GameResults_Section1` | Section 1 results only (used for leaderboard) |
| `GameResults_Section2` | Section 2 results only |
| `GameResults_Section3` | Section 3 results only |
| `UserSessions` | Player session records (name, session ID) |
| `MLPredictions` | ML request/response logs and parsed prediction results |

---

## Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- A MongoDB Atlas cluster (or local MongoDB instance)

### Configuration

Update `appsettings.json` with your MongoDB connection string:

```json
{
  "MongoDB": {
    "ConnectionString": "your-mongodb-connection-string",
    "DatabaseName": "CognitiveOverloadLMS"
  }
}
```

### Run

```bash
dotnet run
```

Then navigate to `https://localhost:{port}` in your browser.

---

## Cognitive Overload Analysis

The system automatically calculates a **cognitive overload score** (0–1 scale) for each game result by measuring behavioral indicators across multiple dimensions. Scores are validated against self-reported cognitive load surveys.

### Behavioral Indicators & Weighting

For each section, the following normalized indicators contribute to the overload calculation:

| Indicator | Description | S1 Weight | S2 Weight | S3 Weight |
|---|---|---|---|---|
| **Heart Rate** | Average beats-per-minute (primary stress indicator) | 20% | 20% | 30% |
| **Hesitation Pauses** | Count of typing/interaction pauses above threshold | 15% | 15% | 15% |
| **Head Tilt** | Number of significant head tilts detected | 10% | 10% | 10% |
| **Mouse Speed** | Mean pixels/second *(inverted: faster = lower load)* | 20% | — | — |
| **Typing Speed** | Mean chars/second *(inverted: faster = lower load)* | — | 20% | 15% |
| **Score** | Game performance *(inverted: higher = lower load)* | — | 10% | 20% |
| **Avg Start Writing** | Avg time-to-first-keystroke per sentence *(S2 only, inverted)* | — | 5% | — |
| **Avg Submit Time** | Avg submission time per sentence *(S2 only, inverted)* | — | 10% | — |
| **Head Movement** | Mean head displacement between frames | 10% | 10% | 10% |
| **Not Completed** | % of game not completed *(S1 only)* | 10% | — | — |

### Calculation Methodology

1. **Extract Behavioral Metrics** – Raw indicators are collected from game telemetry and behavior logs
2. **Compute Per-Section Baseline** – For each section, calculate min, max, and mean values across all games in the session
3. **Normalize Indicators** – Scale each to [0, 1] using min-max normalization: `(value - min) / (max - min)`
   - **Special case**: If baseline min = max (zero variance), normalized value defaults to 0.5
   - **Inverted indicators**: `1 - normalized_value` (for speed & score metrics where higher is better)
4. **Apply Section Weights** – Multiply each normalized indicator by its section-specific weight factor
5. **Weighted Sum** – Sum all weighted terms to produce overload score in [0, 1]
6. **Threshold Classification** – Score > threshold (default 0.5) → game classified as "overloaded"

### Survey Validation

After game completion, players self-rate their cognitive load on a 1–5 Likert scale. This **survey average** enables validation:
- **Survey Overloaded**: `surveyavg > 3.0` (moderate-to-high load reported)
- **Predicted Overloaded**: `overloadScore > threshold` (detected via behavioral signals)
- **Validation Metrics Reported**:
  - **Pearson Correlation**: Continuous correlation between overload score and survey average
  - **Classification Accuracy**: % agreement between predicted and survey-based overload classification

### API Endpoints

#### Full Analysis & Persistence

**GET** `/api/GameData/overload/analysis`

Performs complete cognitive overload analysis for all games across all sessions, with optional persistence and syncing.

**Query Parameters**:
- `persist` (bool): If `true`, save calculated `overloadScore`, `overloaded`, and `surveyOverloaded` fields to MongoDB
- `syncFirst` (bool): If `true`, pre-sync `UserSessions.Games` with `GameResults` before analysis (preserves existing overload data during sync)
- `overloadThreshold` (double): Custom threshold for binary classification; default is 0.5

**Response Example**:
```json
{
  "perGame": [
    {
      "sessionId": "...",
      "gameId": "...",
      "sectionNumber": 2,
      "rawIndicators": { "heartRate": 78, "hesitationPauses": 2, ... },
      "normalizedIndicators": { "heartRate": 0.43, "hesitationPause": 0.5, ... },
      "weightedBreakdown": { "heartRate": 0.086, "hesitationPause": 0.075, ... },
      "overloadScore": 0.52,
      "predictedOverloaded": true,
      "surveyavg": 3.5,
      "surveyOverloaded": true
    },
    ...
  ],
  "baselines": {
    "1": { "heartRate": { "min": 65, "max": 95, "mean": 78 }, ... },
    "2": { ... },
    "3": { ... }
  },
  "validation": {
    "pearsonCorrelation": 0.68,
    "accuracy": 0.72,
    "totalGames": 18,
    "gamesWithSurvey": 15
  }
}
```

#### Single-Game Debug Analysis

**GET** `/api/GameData/overload/debug`

Isolated analysis for a single game to verify calculation math and diagnose zero/unexpected scores.

**Query Parameters**:
- `sessionId` (string, required): Session identifier
- `sectionNumber` (int, required): Section number (1, 2, or 3)
- `gameId` (string, optional): Specific game ID; if omitted, uses latest game in section

**Response Example**:
```json
{
  "sessionId": "...",
  "sectionNumber": 2,
  "gameId": "...",
  "rawIndicators": {
    "heartRate": 78,
    "hesitationPauses": 2,
    "headTilt": 1,
    "typingSpeed": 45.2,
    "score": 400,
    "avgStartWriting": 320,
    "avgSubmitTime": 1200,
    "averageHeadMovement": 8.5
  },
  "baseline": {
    "heartRate": { "min": 65, "max": 95, "mean": 78 },
    "hesitationPauses": { "min": 0, "max": 5, "mean": 2 },
    "headTilt": { "min": 0, "max": 3, "mean": 1 },
    "typingSpeed": { "min": 30, "max": 60, "mean": 45 },
    "score": { "min": 200, "max": 500, "mean": 350 },
    "avgStartWriting": { "min": 200, "max": 500, "mean": 320 },
    "avgSubmitTime": { "min": 800, "max": 1500, "mean": 1100 },
    "averageHeadMovement": { "min": 5, "max": 15, "mean": 10 }
  },
  "normalizedIndicators": {
    "heartRate": 0.43,
    "hesitationPauses": 0.4,
    "headTilt": 0.33,
    "typingSpeed": 0.76,
    "score": 0.67,
    "avgStartWriting": 0.6,
    "avgSubmitTime": 0.58,
    "averageHeadMovement": 0.3
  },
  "weightedTerms": {
    "heartRate": 0.086,
    "hesitationPauses": 0.06,
    "headTilt": 0.033,
    "typingSpeed": 0.20,
    "score": 0.067,
    "avgStartWriting": 0.03,
    "avgSubmitTime": 0.058,
    "averageHeadMovement": 0.03
  },
  "overloadScore": 0.545,
  "isOverloaded": true,
  "threshold": 0.5,
  "warnings": [
    "Zero-variance baseline for hesitationPauses: normalized to 0.5",
    "Behavioral signal present: heartRate=78, hesitationPauses=2, headTilt=1"
  ]
}
```

---

## Leaderboards

Each section has a **Scoreboard** button at the top-right of the page. The modal shows a ranked table of past results fetched live from MongoDB. A **Clear Leaderboard** option is available to reset entries for a fresh study session.

---

## Troubleshooting Overload Scores

**Problem**: All overload scores are 0 or near 0

**Solutions**:
1. Use the **debug endpoint** to inspect a single game's raw indicators, baseline, and normalized values:
   ```bash
   GET /api/GameData/overload/debug?sessionId={sessionId}&sectionNumber={sectionNumber}
   ```
2. Check the `warnings` array in the debug response for:
   - **Zero-variance baseline**: All games have identical values for an indicator → normalized to neutral (0.5)
   - **Missing behavioral signal**: Heart rate, head tilt, and hesitation all zero → no stress detected
3. Ensure you have **3+ games per section** to build meaningful baselines with variance
4. Verify behavioral data collection: Check `rawIndicators` in debug response—if all are 0, behavior logging may not be active

**Problem**: Overload scores don't correlate with survey ratings

1. Check **validation metrics** in the analysis response: Pearson correlation and accuracy indicate alignment
2. Run the full analysis with `persist=false` to compare predicted vs. survey-based overload without modifying data
3. Consider adjusting `overloadThreshold` or section weights if false positive/negative rates are high

---

## Notes

- Head tracking requires webcam permission in the browser
- Head tracking is **enabled by default** in all sections; it can be toggled off in Section 3 if device performance is limited
- The platform is intended for controlled research sessions, not public deployment
- Cognitive overload scores are calculated post-game and stored with the game result for later correlation analysis
- For best overload score accuracy, ensure behavioral data is collected across 3+ games per section (to avoid zero-variance baseline collapse)
- **Section 2 Timing Data**: Per-sentence `timeToFirstKeyMs` and `timeToSubmitAfterDisappearMs` are automatically computed and stored in the `words` telemetry array for detailed cognitive load analysis per sentence
