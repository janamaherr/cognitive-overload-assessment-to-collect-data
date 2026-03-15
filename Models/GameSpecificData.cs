using MongoDB.Bson.Serialization.Attributes;

namespace CognitiveOverloadLMS.Models
{
    public class GameSpecificData
    {
        // Memory Game Data
        [BsonElement("gridSize")]
        [BsonIgnoreIfNull]
        public int? GridSize { get; set; }

        [BsonElement("sequenceShown")]
        [BsonIgnoreIfNull]
        public List<int>? SequenceShown { get; set; }

        [BsonElement("sequenceClicked")]
        [BsonIgnoreIfNull]
        public List<int>? SequenceClicked { get; set; }

        [BsonElement("correctClicks")]
        [BsonIgnoreIfNull]
        public int? CorrectClicks { get; set; }
        
        // Word Scramble Data
        [BsonElement("wordsCompleted")]
        [BsonIgnoreIfNull]
        public List<WordResult>? WordsCompleted { get; set; }
        
        [BsonElement("correctCount")]
        [BsonIgnoreIfNull]
        public int? CorrectCount { get; set; }
        
        [BsonElement("totalWords")]
        [BsonIgnoreIfNull]
        public int? TotalWords { get; set; }
        
        [BsonElement("averageWPM")]
        [BsonIgnoreIfNull]
        public double? AverageWPM { get; set; }
        
        [BsonElement("accuracy")]
        [BsonIgnoreIfNull]
        public double? Accuracy { get; set; }
        
        // Arrow Challenge Data - Simplified
        [BsonElement("score")]
        [BsonIgnoreIfNull]
        public int? Score { get; set; }
        
        [BsonElement("bestScore")]
        [BsonIgnoreIfNull]
        public int? BestScore { get; set; }

        [BsonElement("arrowThrows")]
        [BsonIgnoreIfNull]
        public List<ArrowThrow>? ArrowThrows { get; set; }
    }
    
    public class WordResult
    {
        [BsonElement("word")]
        public string Word { get; set; } = string.Empty;
        
        [BsonElement("scrambled")]
        public string Scrambled { get; set; } = string.Empty;
        
        [BsonElement("userAnswer")]
        public string UserAnswer { get; set; } = string.Empty;
        
        [BsonElement("isCorrect")]
        public bool IsCorrect { get; set; }
        
        [BsonElement("timeTaken")]
        public double TimeTaken { get; set; }
        
        [BsonElement("wpm")]
        public double Wpm { get; set; }
        
        [BsonElement("startTime")]
        public string StartTime { get; set; } = string.Empty;
        
        [BsonElement("endTime")]
        public string EndTime { get; set; } = string.Empty;
    }
    
    public class ArrowThrow
    {
        [BsonElement("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
        
        [BsonElement("angle")]
        public double Angle { get; set; }
        
        [BsonElement("hitAnotherArrow")]
        public bool HitAnotherArrow { get; set; }
        
        [BsonElement("rotationSpeed")]
        public double RotationSpeed { get; set; }
    }
}