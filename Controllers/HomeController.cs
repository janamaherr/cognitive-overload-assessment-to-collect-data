using Microsoft.AspNetCore.Mvc;
using CognitiveOverloadLMS.Models;
using CognitiveOverloadLMS.Services;
using MongoDB.Driver;

namespace CognitiveOverloadLMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly IMongoCollection<UserSession> _sessions;

        public HomeController(MongoDBService mongoDBService)
        {
            _sessions = mongoDBService.GetCollection<UserSession>("UserSessions");
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { success = false, error = "Request body is required." });
                }

                if (string.IsNullOrWhiteSpace(request.FirstName) ||
                    string.IsNullOrWhiteSpace(request.LastName) ||
                    request.Age <= 0 ||
                    string.IsNullOrWhiteSpace(request.Major) ||
                    string.IsNullOrWhiteSpace(request.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new { success = false, error = "All participant fields are required." });
                }

                var normalizedFirstName = request.FirstName.Trim();
                var normalizedLastName = request.LastName.Trim();

                var session = new UserSession
                {
                    UserName = $"{normalizedFirstName} {normalizedLastName}",
                    FirstName = normalizedFirstName,
                    LastName = normalizedLastName,
                    Age = request.Age,
                    Major = request.Major.Trim(),
                    PhoneNumber = request.PhoneNumber.Trim(),
                    Email = request.Email.Trim(),
                    IndexBehaviorData = request.IndexBehaviorData ?? new BehaviorData(),
                    StartTime = DateTime.UtcNow,
                    Games = new List<GameResult>()
                };

                await _sessions.InsertOneAsync(session);

                return Ok(new
                {
                    sessionId = session.Id,
                    success = true,
                    userName = session.UserName
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        public IActionResult Results()
        {
            return View();
        }
    }
}
