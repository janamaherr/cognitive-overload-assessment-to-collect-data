using MongoDB.Bson.Serialization.Attributes;

namespace CognitiveOverloadLMS.Models
{
    public class BehaviorData
    {
        [BsonElement("mouseMovements")]
        public List<MouseMovement> MouseMovements { get; set; } = new();
        
        [BsonElement("typingEvents")]
        public List<TypingEvent> TypingEvents { get; set; } = new();
        
        [BsonElement("hesitationPauses")]
        public List<HesitationPause> HesitationPauses { get; set; } = new();

        [BsonElement("hesitationPauseCount")]
        public int HesitationPauseCount { get; set; }
        
        [BsonElement("averageMouseSpeed")]
        public double AverageMouseSpeed { get; set; }
        
        [BsonElement("averageTypingSpeed")]
        public double AverageTypingSpeed { get; set; }
        
        [BsonElement("averageHeadMovement")]
        public double AverageHeadMovement { get; set; }

        [BsonElement("lookAwayCount")]
        public int LookAwayCount { get; set; }

        [BsonElement("headTiltCount")]
        public int HeadTiltCount { get; set; }
        
        [BsonElement("heartRate")]
        public double HeartRate { get; set; }

        [BsonElement("heartRateDifference")]
        public double? HeartRateDifference { get; set; }

        [BsonElement("heartRatebefore")]
        public int InitialHeartRate { get; set; }
    }
    
    public class MouseMovement
    {
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [BsonElement("x")]
        public int X { get; set; }
        
        [BsonElement("y")]
        public int Y { get; set; }
        
        [BsonElement("speed")]
        public double Speed { get; set; }
        
        [BsonElement("acceleration")]
        public double Acceleration { get; set; }
    }
    
    public class TypingEvent
    {
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [BsonElement("key")]
        public string Key { get; set; } = string.Empty;
        
        [BsonElement("delaySinceLastKeyMs")]
        public int DelaySinceLastKeyMs { get; set; }
        
        [BsonElement("speed")]
        public double Speed { get; set; }
    }
    
    public class HesitationPause
    {
        [BsonElement("startTime")]
        public DateTime StartTime { get; set; }
        
        [BsonElement("endTime")]
        public DateTime EndTime { get; set; }
        
        [BsonElement("durationSeconds")]
        public double DurationSeconds { get; set; }
        
        [BsonElement("location")]
        public string Location { get; set; } = string.Empty;
    }
}