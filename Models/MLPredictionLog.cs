using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CognitiveOverloadLMS.Models
{
    public class MLPredictionLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("sessionId")]
        [BsonIgnoreIfNull]
        public string? SessionId { get; set; }

        [BsonElement("sectionNumber")]
        public int SectionNumber { get; set; }

        [BsonElement("gameType")]
        public int GameType { get; set; }

        [BsonElement("score")]
        public int Score { get; set; }

        [BsonElement("completed")]
        public int Completed { get; set; }

        [BsonElement("prediction")]
        public int Prediction { get; set; }

        [BsonElement("label")]
        public string Label { get; set; } = string.Empty;

        [BsonElement("probability")]
        public double Probability { get; set; }

        [BsonElement("breakdown")]
        [BsonIgnoreIfNull]
        public MLPredictionBreakdown? Breakdown { get; set; }

        [BsonElement("requestPayload")]
        public BsonDocument RequestPayload { get; set; } = new();

        [BsonElement("responsePayload")]
        public BsonDocument ResponsePayload { get; set; } = new();

        [BsonElement("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class MLPredictionBreakdown
    {
        [BsonElement("behavioral")]
        public double Behavioral { get; set; }

        [BsonElement("physiological")]
        public double Physiological { get; set; }

        [BsonElement("contextual")]
        public double Contextual { get; set; }
    }
}
