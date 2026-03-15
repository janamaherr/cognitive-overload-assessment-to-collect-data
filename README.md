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

### Section 2 — Word Scramble Race
Players are given a scrambled word and must type the correct answer within 15 seconds per word.
- Words are drawn from 5 categories: **Animals**, **Fruits**, **Countries**, **Colours**, **Food**
- Each round presents one word from each category (5 words total), in random order
- The current word's category is displayed as a badge
- Leaderboard: ranked by score then number of correct words

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

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 9 MVC (C#) |
| Frontend | Razor Views, Bootstrap 5, vanilla JavaScript |
| Database | MongoDB Atlas |
| Head Tracking | TensorFlow.js 3.21.0 + BlazeFace 0.0.7 |
| Behavior Logging | Custom `BehaviorLogger`, `MouseSpeedTracker`, `HeadTracker` JS classes |

---

## Project Structure

```
Controllers/
  GameDataController.cs     — API endpoints for saving results & leaderboards
  HomeController.cs
  QuestionController.cs

Models/
  GameResult.cs             — Top-level result document saved to MongoDB
  GameSpecificData.cs       — Per-section game metadata (sparse/nullable fields)
  BehaviorData.cs           — Behavioral signal container
  UserSession.cs

Services/
  MongoDBService.cs         — MongoDB connection and collection access

Views/Questions/
  Section1.cshtml           — Memory Pattern Challenge
  Section2.cshtml           — Word Scramble Race
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

## Leaderboards

Each section has a **Scoreboard** button at the top-right of the page. The modal shows a ranked table of past results fetched live from MongoDB. A **Clear Leaderboard** option is available to reset entries for a fresh study session.

---

## Notes

- Head tracking requires webcam permission in the browser
- Head tracking is **enabled by default** in all sections; it can be toggled off in Section 3 if device performance is limited
- The platform is intended for controlled research sessions, not public deployment
