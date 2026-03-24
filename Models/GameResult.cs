using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CognitiveOverloadLMS.Models
{
    public class GameResult
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        [BsonElement("sessionId")]
        public string SessionId { get; set; } = string.Empty;
        
        [BsonElement("gameType")]
        public string GameType { get; set; } = string.Empty;
        
        [BsonElement("sectionNumber")]
        public int SectionNumber { get; set; }
        
        [BsonElement("startTime")]
        public DateTime StartTime { get; set; }
        
        [BsonElement("endTime")]
        public DateTime EndTime { get; set; }
        
        [BsonElement("score")]
        public int Score { get; set; }
        
        [BsonElement("totalTimeSeconds")]
        public double TotalTimeSeconds { get; set; }
        
        [BsonElement("completed")]
        public bool Completed { get; set; }
        
        [BsonElement("behaviorData")]
        public BehaviorData BehaviorData { get; set; } = new();
        
        [BsonElement("gameData")]
        public GameSpecificData GameData { get; set; } = new();

        [BsonElement("words")]
        [BsonIgnoreIfNull]
        public List<WordTelemetry>? Words { get; set; }

        public Boolean Overloaded { get; set; }
    }

    public class WordTelemetry
    {
        [BsonElement("sentenceIndex")]
        public int SentenceIndex { get; set; }

        [BsonElement("wordCount")]
        public int WordCount { get; set; }

        [BsonElement("sentence")]
        public string Sentence { get; set; } = string.Empty;

        [BsonElement("typingStartTime")]
        [BsonIgnoreIfNull]
        public string? TypingStartTime { get; set; }

        [BsonElement("firstKeyTime")]
        [BsonIgnoreIfNull]
        public string? FirstKeyTime { get; set; }

        [BsonElement("submitTime")]
        [BsonIgnoreIfNull]
        public string? SubmitTime { get; set; }

        [BsonElement("timeToFirstKeyMs")]
        public int TimeToFirstKeyMs { get; set; }

        [BsonElement("timeToSubmitAfterDisappearMs")]
        public int TimeToSubmitAfterDisappearMs { get; set; }

        [BsonElement("behaviorData")]
        public WordBehaviorData BehaviorData { get; set; } = new();
    }

    public class WordBehaviorData
    {
        [BsonElement("typingEvents")]
        public List<TypingEvent> TypingEvents { get; set; } = new();

        [BsonElement("hesitationPauses")]
        public List<HesitationPause> HesitationPauses { get; set; } = new();

        [BsonElement("mouseMovements")]
        public List<MouseMovement> MouseMovements { get; set; } = new();

        [BsonElement("headSamples")]
        public List<WordHeadSample> HeadSamples { get; set; } = new();

        [BsonElement("lookAwayCount")]
        public int LookAwayCount { get; set; }

        [BsonElement("headTiltCount")]
        public int HeadTiltCount { get; set; }

        [BsonElement("averageHeadMovement")]
        public double AverageHeadMovement { get; set; }

        [BsonElement("averageTypingSpeed")]
        public double AverageTypingSpeed { get; set; }

        [BsonElement("averageMouseSpeed")]
        public double AverageMouseSpeed { get; set; }
    }

    public class WordHeadSample
    {
        [BsonElement("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [BsonElement("x")]
        public double X { get; set; }

        [BsonElement("y")]
        public double Y { get; set; }

        [BsonElement("z")]
        public double Z { get; set; }

        [BsonElement("movementDelta")]
        public double MovementDelta { get; set; }

        [BsonElement("isLookAway")]
        public bool IsLookAway { get; set; }

        [BsonElement("isHeadTilt")]
        public bool IsHeadTilt { get; set; }
    }
}