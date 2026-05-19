using Microsoft.AspNetCore.Mvc;
using CognitiveOverloadLMS.Models;
using CognitiveOverloadLMS.Services;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace CognitiveOverloadLMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameDataController : ControllerBase
    {
        private readonly IMongoCollection<GameResult> _gameResults;
        private readonly IMongoCollection<GameResult> _section1Results;
        private readonly IMongoCollection<GameResult> _section2Results;
        private readonly IMongoCollection<GameResult> _section3Results;
        private readonly IMongoCollection<UserSession> _userSessions;
        private readonly IMongoCollection<MLPredictionLog> _mlPredictionLogs;
        private readonly ILogger<GameDataController> _logger;

        public GameDataController(MongoDBService mongoDBService, ILogger<GameDataController> logger)
        {
            _gameResults = mongoDBService.GetCollection<GameResult>("GameResults");
            _section1Results = mongoDBService.GetCollection<GameResult>("GameResults_Section1");
            _section2Results = mongoDBService.GetCollection<GameResult>("GameResults_Section2");
            _section3Results = mongoDBService.GetCollection<GameResult>("GameResults_Section3");
            _userSessions = mongoDBService.GetCollection<UserSession>("UserSessions");
            _mlPredictionLogs = mongoDBService.GetCollection<MLPredictionLog>("MLPredictions");
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { 
                success = true, 
                message = "GameData API is working!",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { 
                success = true, 
                message = "pong",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveGameResult([FromBody] GameResult gameResult)
        {
            try
            {
                _logger.LogInformation("========== GAME DATA SAVE ATTEMPT ==========");
                _logger.LogInformation("Received game result for session: {SessionId}", gameResult?.SessionId);
                
                if (gameResult == null)
                {
                    _logger.LogError("GameResult is null");
                    return BadRequest(new { success = false, error = "GameResult is null" });
                }

                // Log the entire object
                _logger.LogInformation("Full GameResult: {@GameResult}", gameResult);

                // Validate required fields
                if (string.IsNullOrEmpty(gameResult.SessionId))
                {
                    _logger.LogError("SessionId is null or empty");
                    return BadRequest(new { success = false, error = "SessionId is required" });
                }

                // Check if session exists
                var session = await _userSessions.Find(s => s.Id == gameResult.SessionId).FirstOrDefaultAsync();
                if (session == null)
                {
                    _logger.LogError("Session not found: {SessionId}", gameResult.SessionId);
                    return BadRequest(new { success = false, error = $"Session not found: {gameResult.SessionId}" });
                }

                // Ensure collections are initialized
                gameResult.BehaviorData ??= new BehaviorData();
                gameResult.GameData ??= new GameSpecificData();
                gameResult.GameData = BuildSectionSpecificGameData(gameResult.SectionNumber, gameResult.GameData);

                gameResult.BehaviorData.HesitationPauseCount = gameResult.BehaviorData.HesitationPauses?.Count ?? 0;
                gameResult.BehaviorData.HeartRateDifference ??= GetHeartRateDifference(gameResult.BehaviorData);
                
                // Calculate total time
                gameResult.TotalTimeSeconds = (gameResult.EndTime - gameResult.StartTime).TotalSeconds;
                
                // Calculate behavior averages
                if (gameResult.BehaviorData?.MouseMovements != null && gameResult.BehaviorData.MouseMovements.Any())
                {
                    gameResult.BehaviorData.AverageMouseSpeed = 
                        gameResult.BehaviorData.MouseMovements.Average(m => m.Speed);
                }
                
                // Save to GameResults collection first
                _logger.LogInformation("Attempting to insert into GameResults collection...");
                await _gameResults.InsertOneAsync(gameResult);
                _logger.LogInformation("Successfully inserted into GameResults with ID: {Id}", gameResult.Id);

                // Save to section-specific collection for easier filtering.
                var sectionCollection = GetSectionCollection(gameResult.SectionNumber);
                if (sectionCollection != null)
                {
                    await sectionCollection.InsertOneAsync(gameResult);
                    _logger.LogInformation("Inserted into section-specific collection for section {SectionNumber}", gameResult.SectionNumber);
                }
                
                // Then add to UserSession's Games array
                _logger.LogInformation("Attempting to update UserSession...");
                var update = Builders<UserSession>.Update.Push(u => u.Games, gameResult);
                var updateResult = await _userSessions.UpdateOneAsync(
                    u => u.Id == gameResult.SessionId,
                    update
                );
                
                _logger.LogInformation("Update result - ModifiedCount: {ModifiedCount}, MatchedCount: {MatchedCount}", 
                    updateResult.ModifiedCount, updateResult.MatchedCount);
                
                if (updateResult.ModifiedCount > 0)
                {
                    _logger.LogInformation("✅ Successfully added game result to UserSession: {SessionId}", gameResult.SessionId);
                }
                else if (updateResult.MatchedCount > 0)
                {
                    _logger.LogWarning("Session found but document not modified. This might mean the game was already in the array?");
                }
                else
                {
                    _logger.LogError("❌ UserSession not found for ID: {SessionId}", gameResult.SessionId);
                }
                
                _logger.LogInformation("========== GAME DATA SAVE COMPLETE ==========");
                
                return Ok(new { success = true, id = gameResult.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving game result: {Message}", ex.Message);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("survey")]
        public async Task<IActionResult> UpdateGameSurvey([FromBody] GameSurveyUpdateRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.GameId))
                {
                    return BadRequest(new { success = false, error = "GameId is required" });
                }

                if (request.SurveyResponses == null)
                {
                    return BadRequest(new { success = false, error = "Survey responses are required" });
                }

                var surveyAvg = Math.Round(
                    (request.SurveyResponses.FrustrationStressAnnoyed
                    + request.SurveyResponses.TimePressure
                    + request.SurveyResponses.MentalEffort
                    + request.SurveyResponses.Success) / 4.0,
                    2);
                var surveyOverloaded = surveyAvg > 3.0;

                var update = Builders<GameResult>.Update
                    .Set(g => g.Surveyavg, surveyAvg)
                    .Set(g => g.SurveyOverloaded, surveyOverloaded)
                    .Set(g => g.PostGameSurvey, request.SurveyResponses);

                var gameFilter = Builders<GameResult>.Filter.Eq(g => g.Id, request.GameId);
                await _gameResults.UpdateOneAsync(gameFilter, update);

                var game = await _gameResults.Find(gameFilter).FirstOrDefaultAsync();
                if (game != null)
                {
                    var sectionCollection = GetSectionCollection(game.SectionNumber);
                    if (sectionCollection != null)
                    {
                        await sectionCollection.UpdateOneAsync(gameFilter, update);
                    }

                    var session = await _userSessions.Find(s => s.Id == game.SessionId).FirstOrDefaultAsync();
                    if (session?.Games != null)
                    {
                        var index = session.Games.FindIndex(existing => IsSameGame(existing, game));
                        if (index >= 0)
                        {
                            session.Games[index].Surveyavg = surveyAvg;
                            session.Games[index].SurveyOverloaded = surveyOverloaded;
                            session.Games[index].PostGameSurvey = request.SurveyResponses;

                            await _userSessions.UpdateOneAsync(
                                s => s.Id == session.Id,
                                Builders<UserSession>.Update.Set(s => s.Games, session.Games));
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    surveyavg = surveyAvg,
                    surveyOverloaded
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating game survey");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSessionResults(string sessionId)
        {
            try
            {
                var results = await _gameResults
                    .Find(r => r.SessionId == sessionId)
                    .ToListAsync();
                    
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
        
        [HttpGet("session/{sessionId}/summary")]
        public async Task<IActionResult> GetSessionSummary(string sessionId)
        {
            try
            {
                var session = await _userSessions
                    .Find(s => s.Id == sessionId)
                    .FirstOrDefaultAsync();
                    
                if (session == null)
                {
                    return NotFound(new { success = false, error = "Session not found" });
                }
                
                return Ok(new { 
                    success = true,
                    userName = session.UserName,
                    firstName = session.FirstName,
                    lastName = session.LastName,
                    age = session.Age,
                    major = session.Major,
                    phoneNumber = session.PhoneNumber,
                    email = session.Email,
                    startTime = session.StartTime,
                    gamesPlayed = session.Games?.Count ?? 0,
                    games = session.Games
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("session/{sessionId}")]
        public async Task<IActionResult> DeleteSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(new { success = false, error = "Session ID is required" });
                }

                // Verify session exists before deleting related records.
                var session = await _userSessions.Find(s => s.Id == sessionId).FirstOrDefaultAsync();
                if (session == null)
                {
                    return NotFound(new { success = false, error = "Session not found" });
                }

                var sessionFilter = Builders<GameResult>.Filter.Eq(r => r.SessionId, sessionId);

                var gameResultsDeleteTask = _gameResults.DeleteManyAsync(sessionFilter);
                var section1DeleteTask = _section1Results.DeleteManyAsync(sessionFilter);
                var section2DeleteTask = _section2Results.DeleteManyAsync(sessionFilter);
                var section3DeleteTask = _section3Results.DeleteManyAsync(sessionFilter);

                await Task.WhenAll(gameResultsDeleteTask, section1DeleteTask, section2DeleteTask, section3DeleteTask);

                var sessionDeleteResult = await _userSessions.DeleteOneAsync(s => s.Id == sessionId);

                _logger.LogInformation(
                    "Deleted session {SessionId}. UserSessions={SessionDeleted}, GameResults={GameResultsDeleted}, Section1={Section1Deleted}, Section2={Section2Deleted}, Section3={Section3Deleted}",
                    sessionId,
                    sessionDeleteResult.DeletedCount,
                    gameResultsDeleteTask.Result.DeletedCount,
                    section1DeleteTask.Result.DeletedCount,
                    section2DeleteTask.Result.DeletedCount,
                    section3DeleteTask.Result.DeletedCount);

                return Ok(new
                {
                    success = true,
                    deleted = new
                    {
                        userSessions = sessionDeleteResult.DeletedCount,
                        gameResults = gameResultsDeleteTask.Result.DeletedCount,
                        gameResultsSection1 = section1DeleteTask.Result.DeletedCount,
                        gameResultsSection2 = section2DeleteTask.Result.DeletedCount,
                        gameResultsSection3 = section3DeleteTask.Result.DeletedCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("leaderboard/section1")]
        public async Task<IActionResult> GetSection1Leaderboard()
        {
            try
            {
                // Fetch only completed runs for Section 1 and sort by best (lowest) total time.
                var winners = await _section1Results
                    .Find(r => r.Completed)
                    .SortBy(r => r.TotalTimeSeconds)
                    .ToListAsync();

                if (!winners.Any())
                {
                    return Ok(new { success = true, leaderboard = Array.Empty<object>() });
                }

                var sessionIds = winners
                    .Where(w => !string.IsNullOrWhiteSpace(w.SessionId))
                    .Select(w => w.SessionId)
                    .Distinct()
                    .ToList();

                var sessions = await _userSessions
                    .Find(s => sessionIds.Contains(s.Id!))
                    .ToListAsync();

                var sessionNameMap = sessions.ToDictionary(s => s.Id!, s => s.UserName);

                var leaderboard = winners
                    .Where(w => sessionNameMap.ContainsKey(w.SessionId))
                    .Select(w => new
                    {
                        playerName = sessionNameMap[w.SessionId],
                        totalTimeSeconds = w.TotalTimeSeconds
                    })
                    .Select((w, index) => new
                    {
                        rank = index + 1,
                        playerName = w.playerName,
                        totalTimeSeconds = w.totalTimeSeconds
                    });

                return Ok(new { success = true, leaderboard });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Section 1 leaderboard");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("leaderboard/section2")]
        public async Task<IActionResult> GetSection2Leaderboard()
        {
            try
            {
                var results = await _section2Results
                    .Find(_ => true)
                    .ToListAsync();

                if (!results.Any())
                {
                    return Ok(new { success = true, leaderboard = Array.Empty<object>() });
                }

                var sessionIds = results
                    .Where(r => !string.IsNullOrWhiteSpace(r.SessionId))
                    .Select(r => r.SessionId)
                    .Distinct()
                    .ToList();

                var sessions = await _userSessions
                    .Find(s => sessionIds.Contains(s.Id!))
                    .ToListAsync();

                var sessionNameMap = sessions.ToDictionary(s => s.Id!, s => s.UserName);

                var ranked = results
                    .Select(r => new
                    {
                        SessionId = r.SessionId,
                        Score = r.Score,
                        CorrectCount = r.GameData?.CorrectCount ?? 0
                    })
                    .OrderByDescending(r => r.Score)
                    .ThenByDescending(r => r.CorrectCount)
                    .ToList();

                var leaderboard = ranked
                    .Where(r => sessionNameMap.ContainsKey(r.SessionId))
                    .Select(r => new
                    {
                        playerName = sessionNameMap[r.SessionId],
                        score = r.Score,
                        correctCount = r.CorrectCount
                    })
                    .Select((r, index) => new
                    {
                        rank = index + 1,
                        playerName = r.playerName,
                        score = r.score,
                        correctCount = r.correctCount
                    });

                return Ok(new { success = true, leaderboard });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Section 2 leaderboard");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("leaderboard/section3")]
        public async Task<IActionResult> GetSection3Leaderboard()
        {
            try
            {
                // Best to worst by score; for ties, lower time ranks higher.
                var results = await _section3Results
                    .Find(_ => true)
                    .SortByDescending(r => r.Score)
                    .ThenBy(r => r.TotalTimeSeconds)
                    .ToListAsync();

                if (!results.Any())
                {
                    return Ok(new { success = true, leaderboard = Array.Empty<object>() });
                }

                var sessionIds = results
                    .Where(r => !string.IsNullOrWhiteSpace(r.SessionId))
                    .Select(r => r.SessionId)
                    .Distinct()
                    .ToList();

                var sessions = await _userSessions
                    .Find(s => sessionIds.Contains(s.Id!))
                    .ToListAsync();

                var sessionNameMap = sessions.ToDictionary(s => s.Id!, s => s.UserName);

                var leaderboard = results
                    .Where(r => sessionNameMap.ContainsKey(r.SessionId))
                    .Select(r => new
                    {
                        playerName = sessionNameMap[r.SessionId],
                        score = r.Score,
                        totalTimeSeconds = r.TotalTimeSeconds
                    })
                    .Select((r, index) => new
                    {
                        rank = index + 1,
                        playerName = r.playerName,
                        score = r.score,
                        totalTimeSeconds = r.totalTimeSeconds
                    });

                return Ok(new { success = true, leaderboard });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Section 3 leaderboard");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("leaderboard/section1")]
        public async Task<IActionResult> ClearSection1Leaderboard()
        {
            await _section1Results.DeleteManyAsync(_ => true);
            return Ok(new { success = true });
        }

        [HttpDelete("leaderboard/section2")]
        public async Task<IActionResult> ClearSection2Leaderboard()
        {
            await _section2Results.DeleteManyAsync(_ => true);
            return Ok(new { success = true });
        }

        [HttpDelete("leaderboard/section3")]
        public async Task<IActionResult> ClearSection3Leaderboard()
        {
            await _section3Results.DeleteManyAsync(_ => true);
            return Ok(new { success = true });
        }

        [HttpGet("overload/analysis")]
        public async Task<IActionResult> AnalyzeOverload(
            [FromQuery] bool persist = false,
            [FromQuery] bool syncFirst = false,
            [FromQuery] double overloadThreshold = 0.5)
        {
            try
            {
                if (overloadThreshold < 0.0 || overloadThreshold > 1.0)
                {
                    return BadRequest(new { success = false, error = "overloadThreshold must be between 0 and 1." });
                }

                // Use UserSessions.Games as source of truth because sessions may be manually corrected.
                var sessions = await _userSessions
                    .Find(_ => true)
                    .ToListAsync();

                var backfillResult = await BackfillHeartRateDifferencesAsync(sessions);
                _logger.LogInformation(
                    "Backfilled heartRateDifference for {UpdatedGames} games across {UpdatedSessions} sessions.",
                    backfillResult.UpdatedGames,
                    backfillResult.UpdatedSessions);

                var allGames = sessions
                    .Where(s => s.Games != null)
                    .SelectMany(s => s.Games!
                        .Where(g => g.SectionNumber >= 1 && g.SectionNumber <= 3)
                        .Select(g =>
                        {
                            // Ensure session linkage exists for downstream updates.
                            if (string.IsNullOrWhiteSpace(g.SessionId))
                            {
                                g.SessionId = s.Id ?? string.Empty;
                            }

                            return g;
                        }))
                    .ToList();

                if (!allGames.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No game records found for sections 1-3.",
                        totalGames = 0
                    });
                }

                var rawBySection = allGames
                    .GroupBy(g => g.SectionNumber)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(BuildRawIndicators).ToList());

                var baselines = rawBySection.ToDictionary(
                    kvp => kvp.Key,
                    kvp => BuildBaseline(kvp.Value));

                foreach (var (section, baseline) in baselines)
                {
                    _logger.LogInformation("=== BASELINE DEBUG (Section {Section}) ===", section);
                    foreach (var (indicator, stats) in baseline)
                    {
                        _logger.LogInformation(
                            "{Indicator} -> Min: {Min}, Max: {Max}, Mean: {Mean}",
                            indicator,
                            stats.Min,
                            stats.Max,
                            stats.Mean);
                    }

                    var relevant = GetWeights(section).Keys.ToList();
                    if (relevant.Any() && relevant.All(indicator => baseline.TryGetValue(indicator, out var s) && s.Min == s.Max))
                    {
                        _logger.LogWarning(
                            "Section {Section} has no variance across all weighted indicators (min == max). Normalized values may collapse to neutral/constant.",
                            section);
                    }
                }

                var perGame = new List<object>();
                var validationPairs = new List<(double score, double surveyAvg, bool predicted, bool surveyOverloaded)>();

                foreach (var game in allGames)
                {
                    var raw = BuildRawIndicators(game);
                    var baseForSection = baselines[game.SectionNumber];
                    var normalizationWarnings = new List<string>();
                    var normalized = NormalizeIndicators(raw, baseForSection, game.SectionNumber, normalizationWarnings);
                    var overloadScore = CalculateWeightedScore(normalized, game.SectionNumber);
                    var predictedOverloaded = overloadScore > overloadThreshold;
                    var hasSurvey = game.Surveyavg > 0;
                    var surveyOverloaded = game.Surveyavg > 3.0;

                    if (HasMissingBehaviorSignal(game))
                    {
                        _logger.LogWarning(
                                "Game {GameId} (section {Section}) appears to have minimal behavior data (heartRateDifference/headTilt/hesitation all zero).",
                            game.Id,
                            game.SectionNumber);
                    }

                    game.OverloadScore = Math.Round(overloadScore, 6);
                    game.Overloaded = predictedOverloaded;
                    game.SurveyOverloaded = hasSurvey && surveyOverloaded;

                    if (persist)
                    {
                        await PersistOverloadResult(game);
                    }

                    if (hasSurvey)
                    {
                        validationPairs.Add((overloadScore, game.Surveyavg, predictedOverloaded, surveyOverloaded));
                    }

                    // Detailed per-game debug logging.
                    _logger.LogInformation("=== GAME DEBUG ===");
                    _logger.LogInformation("Game ID: {GameId}, Section: {Section}", game.Id, game.SectionNumber);
                    _logger.LogInformation(
                        "Raw - HeartRateDifference: {HeartRateDifference}, Hesitation: {Hesitation}, HeadTilt: {HeadTilt}, MouseSpeed: {MouseSpeed}, TypingSpeed: {TypingSpeed}, Score: {Score}",
                        GetIndicator(raw, "heartRateDifference"),
                        GetIndicator(raw, "hesitationPauses"),
                        GetIndicator(raw, "headTilt"),
                        GetIndicator(raw, "mouseSpeed"),
                        GetIndicator(raw, "typingSpeed"),
                        GetIndicator(raw, "score"));

                    if (baseForSection.TryGetValue("heartRateDifference", out var heartRateDifferenceBaseline))
                    {
                        _logger.LogInformation(
                            "Baseline HeartRateDifference - Min: {Min}, Max: {Max}",
                            heartRateDifferenceBaseline.Min,
                            heartRateDifferenceBaseline.Max);
                    }

                    _logger.LogInformation(
                        "Normalized values: {Normalized}",
                        string.Join(", ", normalized.Select(kv => $"{kv.Key}={kv.Value:F4}")));

                    var weights = GetWeights(game.SectionNumber);
                    var weightedBreakdown = string.Join(
                        ", ",
                        weights.Select(w =>
                        {
                            var value = normalized.TryGetValue(w.Key, out var normalizedValue) ? normalizedValue : 0.5;
                            return $"{w.Key}:{value:F4}*{w.Value:F2}={(value * w.Value):F4}";
                        }));
                    _logger.LogInformation("Weighted breakdown: {Breakdown}", weightedBreakdown);

                    if (normalizationWarnings.Count > 0)
                    {
                        foreach (var warning in normalizationWarnings)
                        {
                            _logger.LogWarning("{Warning}", warning);
                        }
                    }

                    _logger.LogInformation(
                        "Final Overload Score: {Score}, Predicted: {Predicted}",
                        overloadScore.ToString("F4"),
                        predictedOverloaded);

                    perGame.Add(new
                    {
                        game.Id,
                        game.SessionId,
                        game.SectionNumber,
                        game.GameType,
                        SurveyAvg = game.Surveyavg,
                        RawIndicators = raw,
                        NormalizedIndicators = normalized,
                        OverloadScore = game.OverloadScore,
                        PredictedOverloaded = game.Overloaded,
                        SurveyOverloaded = hasSurvey ? game.SurveyOverloaded : (bool?)null,
                        HasSurvey = hasSurvey
                    });
                }

                var correlation = CalculatePearsonCorrelation(validationPairs.Select(v => v.score).ToList(), validationPairs.Select(v => v.surveyAvg).ToList());
                var correct = validationPairs.Count(v => v.predicted == v.surveyOverloaded);
                var accuracy = validationPairs.Count > 0 ? (double)correct / validationPairs.Count : 0.0;

                return Ok(new
                {
                    success = true,
                    persist,
                    source = "UserSessions.Games",
                    thresholds = new
                    {
                        overloadScore = overloadThreshold,
                        surveyAvg = 3.0
                    },
                    totalGames = allGames.Count,
                    withSurveyCount = validationPairs.Count,
                    withoutSurveyCount = allGames.Count - validationPairs.Count,
                    validation = new
                    {
                        pearsonCorrelation = correlation,
                        accuracy = Math.Round(accuracy, 6),
                        correct,
                        compared = validationPairs.Count
                    },
                    baselines,
                    perGame
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while analyzing overload");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("overload/debug")]
        public async Task<IActionResult> DebugSingleGameOverload([FromQuery] string sessionId, [FromQuery] int sectionNumber, [FromQuery] string? gameId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(new { success = false, error = "sessionId is required" });
                }

                var sessions = await _userSessions.Find(_ => true).ToListAsync();
                var allGames = sessions
                    .Where(s => s.Games != null)
                    .SelectMany(s => s.Games!
                        .Where(g => g.SectionNumber >= 1 && g.SectionNumber <= 3)
                        .Select(g =>
                        {
                            if (string.IsNullOrWhiteSpace(g.SessionId))
                            {
                                g.SessionId = s.Id ?? string.Empty;
                            }

                            return g;
                        }))
                    .ToList();

                var target = allGames
                    .Where(g => g.SessionId == sessionId && g.SectionNumber == sectionNumber)
                    .Where(g => string.IsNullOrWhiteSpace(gameId) || g.Id == gameId)
                    .OrderByDescending(g => g.StartTime)
                    .FirstOrDefault();

                if (target == null)
                {
                    return NotFound(new { success = false, error = "No matching game found" });
                }

                var sectionRows = allGames
                    .Where(g => g.SectionNumber == sectionNumber)
                    .Select(BuildRawIndicators)
                    .ToList();

                var baseline = BuildBaseline(sectionRows);
                var raw = BuildRawIndicators(target);
                var warnings = new List<string>();
                var normalized = NormalizeIndicators(raw, baseline, sectionNumber, warnings);
                var overloadScore = CalculateWeightedScore(normalized, sectionNumber);

                return Ok(new
                {
                    success = true,
                    sessionId,
                    sectionNumber,
                    gameId = target.Id,
                    raw,
                    baseline,
                    normalized,
                    overloadScore = Math.Round(overloadScore, 6),
                    predictedOverloaded = overloadScore > 0.5,
                    warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in single game overload debug");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        private IMongoCollection<GameResult>? GetSectionCollection(int sectionNumber)
        {
            return sectionNumber switch
            {
                1 => _section1Results,
                2 => _section2Results,
                3 => _section3Results,
                _ => null
            };
        }

        private static GameSpecificData BuildSectionSpecificGameData(int sectionNumber, GameSpecificData source)
        {
            source ??= new GameSpecificData();

            return sectionNumber switch
            {
                // Section 1: Memory Pattern Challenge
                1 => new GameSpecificData
                {
                    GridSize = source.GridSize,
                    SequenceShown = source.SequenceShown,
                    SequenceClicked = source.SequenceClicked,
                    CorrectClicks = source.CorrectClicks
                },

                // Section 2: Sentence Memory Test
                2 => new GameSpecificData
                {
                    WordsCompleted = source.WordsCompleted,
                    CorrectCount = source.CorrectCount,
                    TotalWords = source.TotalWords,
                    SentencesCompleted = source.SentencesCompleted,
                    TotalSentences = source.TotalSentences,
                    TypingSpeedByWordCount = source.TypingSpeedByWordCount,
                    AverageWPM = source.AverageWPM,
                    Accuracy = source.Accuracy,
                    AvgStartWriting = source.AvgStartWriting,
                    AvgSubmitTime = source.AvgSubmitTime
                },

                // Section 3: Twisty Arrow Game
                3 => new GameSpecificData
                {
                    Score = source.Score,
                    BestScore = source.BestScore,
                    ArrowThrows = source.ArrowThrows
                },

                // Fallback: keep incoming data as-is for unknown sections
                _ => source
            };
        }

        private static Dictionary<string, double> BuildRawIndicators(GameResult game)
        {
            var behavior = game.BehaviorData ?? new BehaviorData();
            var words = game.Words ?? new List<WordTelemetry>();
            var avgStartWriting = words.Count > 0 ? words.Average(w => (double)w.TimeToFirstKeyMs) : (game.GameData?.AvgStartWriting ?? 0.0);
            var avgSubmitTime = words.Count > 0 ? words.Average(w => (double)w.TimeToSubmitAfterDisappearMs) : (game.GameData?.AvgSubmitTime ?? 0.0);

            return new Dictionary<string, double>
            {
                ["mouseSpeed"] = behavior.AverageMouseSpeed,
                ["averageMouseSpeed"] = behavior.AverageMouseSpeed,
                ["typingSpeed"] = behavior.AverageTypingSpeed,
                ["averageTypingSpeed"] = behavior.AverageTypingSpeed,
                    ["hesitationPauses"] = behavior.HesitationPauseCount > 0 ? behavior.HesitationPauseCount : (behavior.HesitationPauses?.Count ?? 0),
                ["headTilt"] = behavior.HeadTiltCount,
                ["heartRateDifference"] = GetHeartRateDifference(behavior),
                ["score"] = game.Score,
                // "completed" is intentionally ignored for Section 3 overload scoring.
                ["notCompleted"] = game.SectionNumber == 3 ? 0.0 : (game.Completed ? 0.0 : 1.0),
                ["completed"] = game.Completed ? 1.0 : 0.0,
                ["totalTimeSeconds"] = game.TotalTimeSeconds,
                ["averageHeadMovement"] = behavior.AverageHeadMovement,
                ["typingEventsCount"] = behavior.TypingEvents?.Count ?? 0,
                ["avgStartWriting"] = avgStartWriting,
                ["avgSubmitTime"] = avgSubmitTime
            };
        }

        private static Dictionary<string, BaselineStats> BuildBaseline(List<Dictionary<string, double>> rows)
        {
            var keys = rows.SelectMany(r => r.Keys).Distinct();
            var baseline = new Dictionary<string, BaselineStats>();

            foreach (var key in keys)
            {
                var vals = rows.Select(r => r.TryGetValue(key, out var v) ? v : 0.0).ToList();
                var min = vals.Min();
                var max = vals.Max();
                var mean = vals.Average();

                baseline[key] = new BaselineStats
                {
                    Min = Math.Round(min, 6),
                    Max = Math.Round(max, 6),
                    Mean = Math.Round(mean, 6)
                };
            }

            return baseline;
        }

        private static Dictionary<string, double> NormalizeIndicators(
            Dictionary<string, double> raw,
            Dictionary<string, BaselineStats> baseline,
            int sectionNumber,
            List<string>? warnings = null)
        {
            var invertIndicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mouseSpeed",
                "typingSpeed",
                "score"
            };

            var relevant = GetWeights(sectionNumber).Keys;
            var normalized = new Dictionary<string, double>();

            foreach (var indicator in relevant)
            {
                var value = raw.TryGetValue(indicator, out var rv) ? rv : 0.0;
                if (!baseline.TryGetValue(indicator, out var baseObj))
                {
                    normalized[indicator] = 0.5;
                    warnings?.Add($"Missing baseline for indicator '{indicator}'. Defaulting to 0.5.");
                    continue;
                }

                var min = baseObj.Min;
                var max = baseObj.Max;

                if (max <= min)
                {
                    warnings?.Add($"Baseline min == max for '{indicator}' (min={min}, max={max}). Using 0.5 neutral normalized value.");
                }

                var scaled = MinMaxNormalize(value, min, max);

                if (invertIndicators.Contains(indicator))
                {
                    scaled = 1.0 - scaled;
                }

                normalized[indicator] = Math.Round(scaled, 6);
            }

            return normalized;
        }

        private static Dictionary<string, double> GetWeights(int sectionNumber)
        {
            return sectionNumber switch
            {
                1 => new Dictionary<string, double>
                {
                    ["heartRateDifference"] = 0.20,
                    ["hesitationPauses"] = 0.15,
                    ["headTilt"] = 0.10,
                    ["mouseSpeed"] = 0.20,
                    ["totalTimeSeconds"] = 0.15,
                    ["notCompleted"] = 0.10,
                    ["averageHeadMovement"] = 0.10
                },
                //heartRateDifference → 20%
                //hesitationPauses → 15%
                //headTilt → 10%
                //mouseSpeed → 20%
                //timeTaken → 15%
                //notCompleted → 10%
                //averageHeadMovement → 10%

                2 => new Dictionary<string, double>
                {
                    ["heartRateDifference"] = 0.20,
                    ["hesitationPauses"] = 0.15,
                    ["headTilt"] = 0.10,
                    ["typingSpeed"] = 0.20,
                    ["score"] = 0.10,
                    ["avgStartWriting"] = 0.05,
                    ["avgSubmitTime"] = 0.10,
                    ["averageHeadMovement"] = 0.10  
                },
                //heartRateDifference → 20%
                //hesitationPauses → 15%
                //headTilt → 10%
                //typingSpeed → 20%
                //score → 10%
                //avgStartWriting → 5%
                //avgSubmitTime → 10%
                //averageHeadMovement → 10%

                3 => new Dictionary<string, double>
                {
                    ["heartRateDifference"] = 0.30,
                    ["hesitationPauses"] = 0.15,
                    ["headTilt"] = 0.10,
                    ["score"] = 0.20,
                    ["typingSpeed"] = 0.15,
                    ["averageHeadMovement"] = 0.10  
                    
                },
                //heartRateDifference → 30%
                //hesitationPauses → 15%
                //headTilt → 10%
                //score → 20%
                //Avgtypingspeed → 15%
                //averageHeadMovement → 10%

                _ => new Dictionary<string, double>()
            };
        }

        private static double CalculateWeightedScore(Dictionary<string, double> normalized, int sectionNumber)
        {
            var weights = GetWeights(sectionNumber);
            if (!weights.Any())
            {
                return 0.0;
            }

            var score = 0.0;
            foreach (var (indicator, weight) in weights)
            {
                var value = normalized.TryGetValue(indicator, out var v) ? v : 0.5;
                score += value * weight;
            }

            return Math.Clamp(score, 0.0, 1.0);
        }

        private static double MinMaxNormalize(double value, double min, double max)
        {
            if (max <= min)
            {
                return 0.5;
            }

            var normalized = (value - min) / (max - min);
            return Math.Clamp(normalized, 0.0, 1.0);
        }

        private static double CalculatePearsonCorrelation(List<double> xs, List<double> ys)
        {
            if (xs.Count == 0 || ys.Count == 0 || xs.Count != ys.Count)
            {
                return 0.0;
            }

            var n = xs.Count;
            var meanX = xs.Average();
            var meanY = ys.Average();

            var covariance = 0.0;
            var varX = 0.0;
            var varY = 0.0;

            for (var i = 0; i < n; i++)
            {
                var dx = xs[i] - meanX;
                var dy = ys[i] - meanY;
                covariance += dx * dy;
                varX += dx * dx;
                varY += dy * dy;
            }

            if (varX <= 0 || varY <= 0)
            {
                return 0.0;
            }

            return Math.Round(covariance / Math.Sqrt(varX * varY), 6);
        }

        private async Task PersistOverloadResult(GameResult game)
        {
            var update = Builders<GameResult>.Update
                .Set(g => g.OverloadScore, game.OverloadScore)
                .Set(g => g.Overloaded, game.Overloaded)
                .Set(g => g.SurveyOverloaded, game.SurveyOverloaded);

            var gameFilter = BuildGameMatchFilter(game);

            await _gameResults.UpdateManyAsync(gameFilter, update);

            var sectionCollection = GetSectionCollection(game.SectionNumber);
            if (sectionCollection != null)
            {
                await sectionCollection.UpdateManyAsync(gameFilter, update);
            }

            var session = await _userSessions
                .Find(s => s.Id == game.SessionId)
                .FirstOrDefaultAsync();

            if (session?.Games == null || !session.Games.Any())
            {
                return;
            }

            var index = session.Games.FindIndex(existing => IsSameGame(existing, game));
            if (index < 0)
            {
                return;
            }

            session.Games[index].OverloadScore = game.OverloadScore;
            session.Games[index].Overloaded = game.Overloaded;
            session.Games[index].SurveyOverloaded = game.SurveyOverloaded;

            await _userSessions.UpdateOneAsync(
                s => s.Id == session.Id,
                Builders<UserSession>.Update.Set(s => s.Games, session.Games));
        }

        private static FilterDefinition<GameResult> BuildGameMatchFilter(GameResult game)
        {
            if (!string.IsNullOrWhiteSpace(game.Id))
            {
                return Builders<GameResult>.Filter.Eq(g => g.Id, game.Id);
            }

            return Builders<GameResult>.Filter.And(
                Builders<GameResult>.Filter.Eq(g => g.SessionId, game.SessionId),
                Builders<GameResult>.Filter.Eq(g => g.SectionNumber, game.SectionNumber),
                Builders<GameResult>.Filter.Eq(g => g.GameType, game.GameType),
                Builders<GameResult>.Filter.Eq(g => g.StartTime, game.StartTime));
        }

        private static bool IsSameGame(GameResult existing, GameResult candidate)
        {
            if (!string.IsNullOrWhiteSpace(existing.Id) && !string.IsNullOrWhiteSpace(candidate.Id))
            {
                return existing.Id == candidate.Id;
            }

            return existing.SectionNumber == candidate.SectionNumber
                && existing.GameType == candidate.GameType
                && existing.StartTime == candidate.StartTime;
        }

        private async Task SyncCollectionsFromUserSessions(List<UserSession> sessions)
        {
            var allSessionGames = sessions
                .Where(s => s.Games != null)
                .SelectMany(s => s.Games!
                    .Where(g => g.SectionNumber >= 1 && g.SectionNumber <= 3)
                    .Select(g =>
                    {
                        if (string.IsNullOrWhiteSpace(g.SessionId))
                        {
                            g.SessionId = s.Id ?? string.Empty;
                        }

                        return g;
                    }))
                .ToList();

            foreach (var game in allSessionGames)
            {
                var filter = BuildGameMatchFilter(game);
                var nonOverloadUpdate = BuildNonOverloadUpdate(game);
                await _gameResults.UpdateManyAsync(filter, nonOverloadUpdate);

                var sectionCollection = GetSectionCollection(game.SectionNumber);
                if (sectionCollection != null)
                {
                    await sectionCollection.UpdateManyAsync(filter, nonOverloadUpdate);
                }
            }

            _logger.LogInformation("Synced {Count} games from UserSessions into GameResults + section collections.", allSessionGames.Count);
        }

        private static double GetIndicator(Dictionary<string, double> raw, string key)
        {
            return raw.TryGetValue(key, out var value) ? value : 0.0;
        }

        private static double GetHeartRateDifference(BehaviorData behavior)
        {
            if (behavior.HeartRateDifference.HasValue)
            {
                return behavior.HeartRateDifference.Value;
            }

            return (behavior.HeartRate ?? 0) - (behavior.InitialHeartRate ?? 0);
        }

        private async Task<HeartRateBackfillResult> BackfillHeartRateDifferencesAsync(List<UserSession> sessions)
        {
            var gameWrites = new List<WriteModel<GameResult>>();
            var sectionWrites = new Dictionary<int, List<WriteModel<GameResult>>>();
            var sessionWrites = new List<WriteModel<UserSession>>();
            var updatedGames = 0;
            var updatedSessions = 0;

            foreach (var session in sessions)
            {
                if (session.Games == null)
                {
                    continue;
                }

                var sessionChanged = false;

                foreach (var game in session.Games)
                {
                    game.BehaviorData ??= new BehaviorData();

                    var expectedDifference = GetHeartRateDifference(game.BehaviorData);
                    if (game.BehaviorData.HeartRateDifference.HasValue
                        && Math.Abs(game.BehaviorData.HeartRateDifference.Value - expectedDifference) < 0.000001)
                    {
                        continue;
                    }

                    game.BehaviorData.HeartRateDifference = expectedDifference;
                    sessionChanged = true;
                    updatedGames++;

                    var filter = BuildGameMatchFilter(game);
                    var update = Builders<GameResult>.Update.Set(g => g.BehaviorData.HeartRateDifference, expectedDifference);

                    gameWrites.Add(new UpdateOneModel<GameResult>(filter, update) { IsUpsert = false });

                    if (!sectionWrites.TryGetValue(game.SectionNumber, out var sectionWriteModels))
                    {
                        sectionWriteModels = new List<WriteModel<GameResult>>();
                        sectionWrites[game.SectionNumber] = sectionWriteModels;
                    }

                    sectionWriteModels.Add(new UpdateOneModel<GameResult>(filter, update) { IsUpsert = false });
                }

                if (sessionChanged)
                {
                    updatedSessions++;
                    sessionWrites.Add(new UpdateOneModel<UserSession>(
                        Builders<UserSession>.Filter.Eq(s => s.Id, session.Id),
                        Builders<UserSession>.Update.Set(s => s.Games, session.Games))
                    {
                        IsUpsert = false
                    });
                }
            }

            await ExecuteBulkWriteInBatches(_gameResults, gameWrites);

            foreach (var (sectionNumber, writes) in sectionWrites)
            {
                var sectionCollection = GetSectionCollection(sectionNumber);
                if (sectionCollection != null)
                {
                    await ExecuteBulkWriteInBatches(sectionCollection, writes);
                }
            }

            await ExecuteBulkWriteInBatches(_userSessions, sessionWrites);

            return new HeartRateBackfillResult
            {
                UpdatedGames = updatedGames,
                UpdatedSessions = updatedSessions
            };
        }

        private static async Task ExecuteBulkWriteInBatches<T>(IMongoCollection<T> collection, List<WriteModel<T>> writes)
        {
            if (writes.Count == 0)
            {
                return;
            }

            const int batchSize = 500;

            for (var index = 0; index < writes.Count; index += batchSize)
            {
                var batch = writes.Skip(index).Take(batchSize).ToList();
                await collection.BulkWriteAsync(batch, new BulkWriteOptions { IsOrdered = false });
            }
        }

        private sealed class HeartRateBackfillResult
        {
            public int UpdatedGames { get; set; }
            public int UpdatedSessions { get; set; }
        }

        private static bool HasMissingBehaviorSignal(GameResult game)
        {
            var behavior = game.BehaviorData ?? new BehaviorData();
            return GetHeartRateDifference(behavior) == 0
                && behavior.HeadTiltCount == 0
                && behavior.HesitationPauseCount == 0
                && (behavior.HesitationPauses?.Count ?? 0) == 0;
        }

        private static UpdateDefinition<GameResult> BuildNonOverloadUpdate(GameResult game)
        {
            return Builders<GameResult>.Update
                .Set(g => g.SessionId, game.SessionId)
                .Set(g => g.GameType, game.GameType)
                .Set(g => g.SectionNumber, game.SectionNumber)
                .Set(g => g.StartTime, game.StartTime)
                .Set(g => g.EndTime, game.EndTime)
                .Set(g => g.Score, game.Score)
                .Set(g => g.TotalTimeSeconds, game.TotalTimeSeconds)
                .Set(g => g.Completed, game.Completed)
                .Set(g => g.BehaviorData, game.BehaviorData)
                .Set(g => g.GameData, game.GameData)
                .Set(g => g.Words, game.Words)
                .Set(g => g.Surveyavg, game.Surveyavg)
                .Set(g => g.PostGameSurvey, game.PostGameSurvey)
                .Set(g => g.MLPrediction, game.MLPrediction);
        }

        private sealed class BaselineStats
        {
            public double Min { get; set; }
            public double Max { get; set; }
            public double Mean { get; set; }
        }

        public sealed class GameSurveyUpdateRequest
        {
            public string GameId { get; set; } = string.Empty;
            public PostGameSurvey? SurveyResponses { get; set; }
        }
[HttpPost("ml-predict")]
public async Task<IActionResult> MLPredict([FromBody] JsonElement body)
{
    try
    {
        using var requestDocument = JsonDocument.Parse(body.GetRawText());
        var requestRoot = requestDocument.RootElement;
        string result;
        var mlCallSucceeded = false;

        try
        {
            using var client = new HttpClient();
            var content = new StringContent(
                body.GetRawText(),  // send as-is, no snake_case transform
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync("http://localhost:8000/predict", content);
            var rawResult = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                result = JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "ML service request failed",
                    statusCode = (int)response.StatusCode,
                    details = rawResult
                });
            }
            else
            {
                result = rawResult;
                mlCallSucceeded = true;
            }
        }
        catch (Exception ex)
        {
            result = JsonSerializer.Serialize(new
            {
                success = false,
                error = "ML service unavailable",
                details = ex.Message
            });
        }

        // Parse ML response
        GameMLPrediction? gamePrediction = null;
        MLPredictionBreakdown? breakdown = null;
        GameMLPredictionBreakdown? gameBreakdown = null;

        if (TryParseJson(result, out var responseDocument))
        {
            var responseRoot = responseDocument.RootElement;
            if (responseRoot.TryGetProperty("breakdown", out var breakdownRoot) && breakdownRoot.ValueKind == JsonValueKind.Object)
            {
                breakdown = new MLPredictionBreakdown
                {
                    Behavioral    = TryGetDouble(breakdownRoot, "behavioral"),
                    Physiological = TryGetDouble(breakdownRoot, "physiological"),
                    Contextual    = TryGetDouble(breakdownRoot, "contextual")
                };

                gameBreakdown = new GameMLPredictionBreakdown
                {
                    Behavioral    = breakdown.Behavioral,
                    Physiological = breakdown.Physiological,
                    Contextual    = breakdown.Contextual
                };
            }

            gamePrediction = new GameMLPrediction
            {
                Prediction  = TryGetInt(responseRoot, "prediction"),
                Probability = TryGetDouble(responseRoot, "probability"),
                Label       = TryGetString(responseRoot, "label") ?? string.Empty,
                Breakdown   = gameBreakdown
            };
        }

        // Log to MLPredictions collection
        var log = new MLPredictionLog
        {
            SessionId      = TryGetString(requestRoot, "sessionId"),
            SectionNumber  = TryGetInt(requestRoot, "sectionNumber"),
            GameType       = TryGetInt(requestRoot, "gameType"),
            Score          = TryGetInt(requestRoot, "score"),
            Completed      = TryGetInt(requestRoot, "completed"),
            Prediction     = gamePrediction?.Prediction ?? 0,
            Probability    = gamePrediction?.Probability ?? 0,
            Label          = gamePrediction?.Label ?? (mlCallSucceeded ? string.Empty : "PredictionUnavailable"),
            Breakdown      = breakdown,
            RequestPayload = BsonDocument.Parse(body.GetRawText()),
            ResponsePayload = SafeParseBson(result),
            CreatedAtUtc   = DateTime.UtcNow
        };

        await _mlPredictionLogs.InsertOneAsync(log);

        // Save prediction back to the game result in MongoDB
        var gameId = TryGetString(requestRoot, "gameId");
        var targetGame = await ResolveTargetGameForPredictionAsync(requestRoot, gameId);

        if (targetGame != null && gamePrediction != null)
        {
            var gameFilter = BuildGameMatchFilter(targetGame);

            await _gameResults.UpdateOneAsync(gameFilter,
                Builders<GameResult>.Update.Set(g => g.MLPrediction, gamePrediction));

            var sectionCollection = GetSectionCollection(targetGame.SectionNumber);
            if (sectionCollection != null)
            {
                await sectionCollection.UpdateOneAsync(gameFilter,
                    Builders<GameResult>.Update.Set(g => g.MLPrediction, gamePrediction));
            }

            // Update UserSession too
            var session = await _userSessions.Find(s => s.Id == targetGame.SessionId).FirstOrDefaultAsync();
            if (session?.Games != null)
            {
                var index = session.Games.FindIndex(existing => IsSameGame(existing, targetGame));
                if (index >= 0)
                {
                    session.Games[index].MLPrediction = gamePrediction;
                    await _userSessions.UpdateOneAsync(
                        s => s.Id == session.Id,
                        Builders<UserSession>.Update.Set(s => s.Games, session.Games));
                }
            }
        }

        if (!mlCallSucceeded)
            return StatusCode(503, result);

        return Content(result, "application/json");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in ML prediction endpoint");
        return BadRequest(new { success = false, error = ex.Message });
    }
}

[HttpGet("ml-predict-retroactive")]
public Task<IActionResult> MLPredictRetroactiveGet(
    [FromQuery] string sessionId,
    [FromQuery] int? gameIndex = null,
    [FromQuery] string? gameId = null,
    [FromQuery] int? sectionNumber = null)
{
    return MLPredictRetroactiveCore(sessionId, gameIndex, gameId, sectionNumber);
}

[HttpPost("ml-predict-retroactive")]
public Task<IActionResult> MLPredictRetroactivePost(
    [FromQuery] string sessionId,
    [FromQuery] int? gameIndex = null,
    [FromQuery] string? gameId = null,
    [FromQuery] int? sectionNumber = null)
{
    return MLPredictRetroactiveCore(sessionId, gameIndex, gameId, sectionNumber);
}

private async Task<IActionResult> MLPredictRetroactiveCore(
    string sessionId,
    int? gameIndex,
    string? gameId,
    int? sectionNumber)
{
    try
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { success = false, error = "sessionId is required" });
        }

        var session = await _userSessions.Find(s => s.Id == sessionId).FirstOrDefaultAsync();
        if (session?.Games == null || session.Games.Count == 0)
        {
            return NotFound(new { success = false, error = "No games found for session" });
        }

        GameResult? targetGame = null;

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            targetGame = session.Games.FirstOrDefault(g => g.Id == gameId)
                ?? await _gameResults.Find(g => g.Id == gameId).FirstOrDefaultAsync();
        }

        if (targetGame == null && gameIndex.HasValue)
        {
            if (gameIndex.Value >= 0 && gameIndex.Value < session.Games.Count)
            {
                targetGame = session.Games[gameIndex.Value];
            }
        }

        if (targetGame == null && sectionNumber.HasValue)
        {
            targetGame = session.Games
                .Where(g => g.SectionNumber == sectionNumber.Value)
                .OrderByDescending(g => g.EndTime)
                .FirstOrDefault();
        }

        if (targetGame == null)
        {
            targetGame = session.Games
                .OrderByDescending(g => g.EndTime)
                .FirstOrDefault();
        }

        if (targetGame == null)
        {
            return NotFound(new { success = false, error = "Game not found" });
        }

        var b = targetGame.BehaviorData ?? new BehaviorData();

        var payload = new
        {
            gameId              = targetGame.Id,
            sessionId            = targetGame.SessionId,
            mouseMovementsCount  = (double)(b.MouseMovements?.Count ?? 0),
            averageMouseSpeed    = b.AverageMouseSpeed,
            typingEventsCount    = (double)(b.TypingEvents?.Count ?? 0),
            averageTypingSpeed   = b.AverageTypingSpeed,
            hesitationPausesCount = (double)(b.HesitationPauseCount),
            averageHeadMovement  = b.AverageHeadMovement,
            lookAwayCount        = (double)(b.LookAwayCount),
            headTiltCount        = (double)(b.HeadTiltCount),
            totalTimeSeconds     = targetGame.TotalTimeSeconds,
            heartRate            = b.HeartRate,
            heartRatebefore      = b.InitialHeartRate,
            heartRateDifference  = b.HeartRateDifference,
            age                  = session.Age,
            gameType             = targetGame.SectionNumber,
            sectionNumber        = targetGame.SectionNumber,
            score                = (double)targetGame.Score,
            completed            = targetGame.Completed ? 1 : 0
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var client = new HttpClient();
        var response = await client.PostAsync("http://localhost:8000/predict", content);
        var result = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(
            "Retroactive ML prediction for session {SessionId}, game {GameId}: {Result}",
            sessionId,
            targetGame.Id,
            result);

        GameMLPrediction? gamePrediction = null;
        MLPredictionBreakdown? breakdown = null;
        GameMLPredictionBreakdown? gameBreakdown = null;

        if (TryParseJson(result, out var responseDocument))
        {
            var responseRoot = responseDocument.RootElement;
            if (responseRoot.TryGetProperty("breakdown", out var breakdownRoot) && breakdownRoot.ValueKind == JsonValueKind.Object)
            {
                breakdown = new MLPredictionBreakdown
                {
                    Behavioral    = TryGetDouble(breakdownRoot, "behavioral"),
                    Physiological = TryGetDouble(breakdownRoot, "physiological"),
                    Contextual    = TryGetDouble(breakdownRoot, "contextual")
                };

                gameBreakdown = new GameMLPredictionBreakdown
                {
                    Behavioral    = breakdown.Behavioral,
                    Physiological = breakdown.Physiological,
                    Contextual    = breakdown.Contextual
                };
            }

            gamePrediction = new GameMLPrediction
            {
                Prediction  = TryGetInt(responseRoot, "prediction"),
                Probability = TryGetDouble(responseRoot, "probability"),
                Label       = TryGetString(responseRoot, "label") ?? string.Empty,
                Breakdown   = gameBreakdown
            };
        }

        var log = new MLPredictionLog
        {
            SessionId      = targetGame.SessionId,
            SectionNumber  = targetGame.SectionNumber,
            GameType       = targetGame.SectionNumber,
            Score          = targetGame.Score,
            Completed      = targetGame.Completed ? 1 : 0,
            Prediction     = gamePrediction?.Prediction ?? 0,
            Probability    = gamePrediction?.Probability ?? 0,
            Label          = gamePrediction?.Label ?? string.Empty,
            Breakdown      = breakdown,
            RequestPayload = BsonDocument.Parse(json),
            ResponsePayload = SafeParseBson(result),
            CreatedAtUtc   = DateTime.UtcNow
        };

        await _mlPredictionLogs.InsertOneAsync(log);

        if (gamePrediction != null)
        {
            var gameFilter = BuildGameMatchFilter(targetGame);

            await _gameResults.UpdateOneAsync(gameFilter,
                Builders<GameResult>.Update.Set(g => g.MLPrediction, gamePrediction));

            var sectionCollection = GetSectionCollection(targetGame.SectionNumber);
            if (sectionCollection != null)
            {
                await sectionCollection.UpdateOneAsync(gameFilter,
                    Builders<GameResult>.Update.Set(g => g.MLPrediction, gamePrediction));
            }

            var sessionToUpdate = await _userSessions.Find(s => s.Id == targetGame.SessionId).FirstOrDefaultAsync();
            if (sessionToUpdate?.Games != null)
            {
                var index = sessionToUpdate.Games.FindIndex(existing => IsSameGame(existing, targetGame));
                if (index >= 0)
                {
                    sessionToUpdate.Games[index].MLPrediction = gamePrediction;
                    await _userSessions.UpdateOneAsync(
                        s => s.Id == sessionToUpdate.Id,
                        Builders<UserSession>.Update.Set(s => s.Games, sessionToUpdate.Games));
                }
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, result);
        }

        return Ok(new
        {
            success = true,
            sessionId,
            gameId = targetGame.Id,
            result = gamePrediction,
            raw = result
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Retroactive ML prediction failed");
        return BadRequest(new { success = false, error = ex.Message });
    }
}

        private static bool TryParseJson(string json, out JsonDocument document)
        {
            try
            {
                document = JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                document = default!;
                return false;
            }
        }

        private static BsonDocument SafeParseBson(string payload)
        {
            try
            {
                return BsonDocument.Parse(payload);
            }
            catch
            {
                return new BsonDocument { ["raw"] = payload };
            }
        }

        private static int TryGetInt(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            return 0;
        }

        private static double TryGetDouble(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            return 0;
        }

        private static string? TryGetString(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }

        private async Task<GameResult?> ResolveTargetGameForPredictionAsync(JsonElement requestRoot, string? gameId)
        {
            if (!string.IsNullOrWhiteSpace(gameId))
            {
                var byId = await _gameResults.Find(Builders<GameResult>.Filter.Eq(g => g.Id, gameId)).FirstOrDefaultAsync();
                if (byId != null)
                {
                    return byId;
                }
            }

            var sessionId = TryGetString(requestRoot, "sessionId");
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return null;
            }

            var sectionNumber = TryGetInt(requestRoot, "sectionNumber");
            var score = TryGetInt(requestRoot, "score");
            var completed = TryGetInt(requestRoot, "completed") == 1;

            var filter = Builders<GameResult>.Filter.And(
                Builders<GameResult>.Filter.Eq(g => g.SessionId, sessionId),
                Builders<GameResult>.Filter.Eq(g => g.SectionNumber, sectionNumber),
                Builders<GameResult>.Filter.Eq(g => g.Score, score),
                Builders<GameResult>.Filter.Eq(g => g.Completed, completed));

            var bySessionAndSection = await _gameResults
                .Find(filter)
                .SortByDescending(g => g.EndTime)
                .FirstOrDefaultAsync();

            if (bySessionAndSection != null)
            {
                return bySessionAndSection;
            }

            return await _gameResults
                .Find(g => g.SessionId == sessionId && g.SectionNumber == sectionNumber)
                .SortByDescending(g => g.EndTime)
                .FirstOrDefaultAsync();
        }

        private string TransformPayloadToSnakeCase(JsonElement body)
        {
            using var document = JsonDocument.Parse(body.GetRawText());
            var root = document.RootElement;

            var snakeCaseDict = new Dictionary<string, object?>();

            foreach (var property in root.EnumerateObject())
            {
                var snakeCaseKey = ToSnakeCase(property.Name);

                // Parse value, preserving nulls
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.Number => property.Value.TryGetDouble(out var d) ? (object)d : property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => property.Value.GetString(),
                    _ => property.Value.ToString()
                };

                snakeCaseDict[snakeCaseKey] = value;
            }

            return JsonSerializer.Serialize(snakeCaseDict);
        }

        private string ToSnakeCase(string camelCase)
        {
            var sb = new StringBuilder();
            foreach (var c in camelCase)
            {
                if (char.IsUpper(c))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        public static int? LatestHeartRate = null;
public static DateTime? LatestHRTimestamp = null;

public class HRPushRequest
{
    public int HeartRate { get; set; }
    public string? SessionId { get; set; }
}

[HttpPost("heartrate-push")]
public IActionResult PushHeartRate([FromBody] HRPushRequest req)
{
    GameDataController.LatestHeartRate   = req.HeartRate;
    GameDataController.LatestHRTimestamp = DateTime.UtcNow;

    _logger.LogInformation("HR received: {HR} bpm", req.HeartRate);

    return Ok(new {
        success   = true,
        heartRate = req.HeartRate,
        timestamp = DateTime.UtcNow
    });
}

[HttpGet("latest-hr")]
public IActionResult GetLatestHR()
{
    return Ok(new {
        heartRate  = GameDataController.LatestHeartRate,
        timestamp  = GameDataController.LatestHRTimestamp,
        ageSeconds = GameDataController.LatestHRTimestamp.HasValue
            ? (DateTime.UtcNow - GameDataController.LatestHRTimestamp.Value)
              .TotalSeconds
            : (double?)null
    });
}
    }
}