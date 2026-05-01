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
                    request.Age <= 0 ||
                    string.IsNullOrWhiteSpace(request.Major))
                {
                    return BadRequest(new { success = false, error = "First name, age, and major are required." });
                }

                var normalizedFirstName = request.FirstName.Trim();
                var normalizedLastName = string.IsNullOrWhiteSpace(request.LastName) ? string.Empty : request.LastName.Trim();
                var userName = string.IsNullOrWhiteSpace(normalizedLastName)
                    ? normalizedFirstName
                    : $"{normalizedFirstName} {normalizedLastName}";

                var session = new UserSession
                {
                    UserName = userName,
                    FirstName = normalizedFirstName,
                    LastName = normalizedLastName,
                    Age = request.Age,
                    Major = request.Major.Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? string.Empty : request.PhoneNumber.Trim(),
                    Email = string.IsNullOrWhiteSpace(request.Email) ? string.Empty : request.Email.Trim(),
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
