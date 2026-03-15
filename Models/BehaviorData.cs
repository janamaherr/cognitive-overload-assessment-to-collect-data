using MongoDB.Bson.Serialization.Attributes;

namespace CognitiveOverloadLMS.Models
{
    public class BehaviorData
    {
        [BsonElement("mouseMovements")]
        public List<MouseMovement> MouseMovements { get; set; } = new();
        
        [BsonElement("typingEvents")]
        public List<TypingEvent> TypingEvents { get; set; } = new();
        
        [BsonElement("headPositions")]
        public List<HeadPosition> HeadPositions { get; set; } = new();
        
        [BsonElement("hesitationPauses")]
        public List<HesitationPause> HesitationPauses { get; set; } = new();
        
        [BsonElement("averageMouseSpeed")]
        public double AverageMouseSpeed { get; set; }
        
        [BsonElement("averageTypingSpeed")]
        public double AverageTypingSpeed { get; set; }
        
        [BsonElement("averageHeadMovement")]
        public double AverageHeadMovement { get; set; }
        
        
        [BsonElement("heartRate")]
        public double HeartRate { get; set; }
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
    
    public class HeadPosition
    {
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [BsonElement("x")]
        public double X { get; set; }
        
        [BsonElement("y")]
        public double Y { get; set; }
        
        [BsonElement("z")]
        public double Z { get; set; }
        
        [BsonElement("movementDelta")]
        public double MovementDelta { get; set; }
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