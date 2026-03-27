using Microsoft.AspNetCore.Mvc;
using CognitiveOverloadLMS.Models;
using CognitiveOverloadLMS.Services;
using MongoDB.Driver;
using System.Linq;
using System.Collections.Generic;

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
        private readonly ILogger<GameDataController> _logger;

        public GameDataController(MongoDBService mongoDBService, ILogger<GameDataController> logger)
        {
            _gameResults = mongoDBService.GetCollection<GameResult>("GameResults");
            _section1Results = mongoDBService.GetCollection<GameResult>("GameResults_Section1");
            _section2Results = mongoDBService.GetCollection<GameResult>("GameResults_Section2");
            _section3Results = mongoDBService.GetCollection<GameResult>("GameResults_Section3");
            _userSessions = mongoDBService.GetCollection<UserSession>("UserSessions");
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
        public async Task<IActionResult> AnalyzeOverload([FromQuery] bool persist = false)
        {
            try
            {
                var allGames = await _gameResults
                    .Find(r => r.SectionNumber >= 1 && r.SectionNumber <= 3)
                    .ToListAsync();

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

                var perGame = new List<object>();
                var validationPairs = new List<(double score, double surveyAvg, bool predicted, bool surveyOverloaded)>();

                foreach (var game in allGames)
                {
                    var raw = BuildRawIndicators(game);
                    var baseForSection = baselines[game.SectionNumber];
                    var normalized = NormalizeIndicators(raw, baseForSection, game.SectionNumber);
                    var overloadScore = CalculateWeightedScore(normalized, game.SectionNumber);
                    var predictedOverloaded = overloadScore > 0.5;
                    var hasSurvey = game.Surveyavg > 0;
                    var surveyOverloaded = game.Surveyavg > 3.0;

                    game.OverloadScore = Math.Round(overloadScore, 6);
                    game.Overloaded = predictedOverloaded;

                    if (persist)
                    {
                        await PersistOverloadResult(game);
                    }

                    if (hasSurvey)
                    {
                        validationPairs.Add((overloadScore, game.Surveyavg, predictedOverloaded, surveyOverloaded));
                    }

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
                        SurveyOverloaded = hasSurvey ? surveyOverloaded : (bool?)null,
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
                    thresholds = new
                    {
                        overloadScore = 0.5,
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
                    Accuracy = source.Accuracy
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

            return new Dictionary<string, double>
            {
                ["mouseSpeed"] = behavior.AverageMouseSpeed,
                ["typingSpeed"] = behavior.AverageTypingSpeed,
                ["hesitationPauses"] = behavior.HesitationPauses?.Count ?? 0,
                ["headTilt"] = behavior.HeadTiltCount,
                ["heartRate"] = behavior.HeartRate,
                ["score"] = game.Score,
                ["notCompleted"] = game.Completed ? 0.0 : 1.0,
                ["typingEventsCount"] = behavior.TypingEvents?.Count ?? 0
            };
        }

        private static Dictionary<string, object> BuildBaseline(List<Dictionary<string, double>> rows)
        {
            var keys = rows.SelectMany(r => r.Keys).Distinct();
            var baseline = new Dictionary<string, object>();

            foreach (var key in keys)
            {
                var vals = rows.Select(r => r.TryGetValue(key, out var v) ? v : 0.0).ToList();
                var min = vals.Min();
                var max = vals.Max();
                var mean = vals.Average();

                baseline[key] = new
                {
                    min = Math.Round(min, 6),
                    max = Math.Round(max, 6),
                    mean = Math.Round(mean, 6)
                };
            }

            return baseline;
        }

        private static Dictionary<string, double> NormalizeIndicators(
            Dictionary<string, double> raw,
            Dictionary<string, object> baseline,
            int sectionNumber)
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
                    continue;
                }

                var minProp = baseObj.GetType().GetProperty("min");
                var maxProp = baseObj.GetType().GetProperty("max");
                var min = minProp != null ? (double)minProp.GetValue(baseObj)! : 0.0;
                var max = maxProp != null ? (double)maxProp.GetValue(baseObj)! : 0.0;

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
                    ["mouseSpeed"] = 0.30,
                    ["hesitationPauses"] = 0.25,
                    ["headTilt"] = 0.20,
                    ["notCompleted"] = 0.15,
                    ["heartRate"] = 0.10
                },
                2 => new Dictionary<string, double>
                {
                    ["typingSpeed"] = 0.30,
                    ["hesitationPauses"] = 0.25,
                    ["headTilt"] = 0.20,
                    ["score"] = 0.15,
                    ["heartRate"] = 0.10
                },
                3 => new Dictionary<string, double>
                {
                    ["headTilt"] = 0.30,
                    ["heartRate"] = 0.25,
                    ["notCompleted"] = 0.20,
                    ["typingEventsCount"] = 0.15,
                    ["score"] = 0.10
                },
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
                .Set(g => g.Overloaded, game.Overloaded);

            await _gameResults.UpdateOneAsync(g => g.Id == game.Id, update);

            var sectionCollection = GetSectionCollection(game.SectionNumber);
            if (sectionCollection != null)
            {
                await sectionCollection.UpdateOneAsync(g => g.Id == game.Id, update);
            }

            var sessionUpdate = Builders<UserSession>.Update
                .Set("games.$.overloadScore", game.OverloadScore)
                .Set("games.$.overloaded", game.Overloaded);

            await _userSessions.UpdateOneAsync(
                s => s.Id == game.SessionId && s.Games.Any(g => g.Id == game.Id),
                sessionUpdate);
        }
    }
}