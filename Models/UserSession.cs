using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CognitiveOverloadLMS.Models
{
    public class UserSession
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userName")]
        public string UserName { get; set; } = string.Empty;

        [BsonElement("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("lastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("age")]
        public int Age { get; set; }

        [BsonElement("major")]
        public string Major { get; set; } = string.Empty;

        [BsonElement("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("indexBehaviorData")]
        public BehaviorData IndexBehaviorData { get; set; } = new();

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<GameResult> Games { get; set; } = new();
    }
}
