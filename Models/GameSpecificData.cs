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

        // Sentence Memory Data (Section 2)
        [BsonElement("sentencesCompleted")]
        [BsonIgnoreIfNull]
        public List<SentenceResult>? SentencesCompleted { get; set; }

        [BsonElement("totalSentences")]
        [BsonIgnoreIfNull]
        public int? TotalSentences { get; set; }

        [BsonElement("typingSpeedByWordCount")]
        [BsonIgnoreIfNull]
        public List<TypingSpeedByWordCount>? TypingSpeedByWordCount { get; set; }
        
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

    public class SentenceResult
    {
        [BsonElement("wordCount")]
        public int WordCount { get; set; }

        [BsonElement("original")]
        public string Original { get; set; } = string.Empty;

        [BsonElement("userAnswer")]
        public string UserAnswer { get; set; } = string.Empty;

        [BsonElement("isCorrect")]
        public bool IsCorrect { get; set; }

        [BsonElement("startTime")]
        public string StartTime { get; set; } = string.Empty;

        [BsonElement("shownTime")]
        public string ShownTime { get; set; } = string.Empty;

        [BsonElement("typingStartTime")]
        [BsonIgnoreIfNull]
        public string? TypingStartTime { get; set; }

        [BsonElement("firstKeyTime")]
        [BsonIgnoreIfNull]
        public string? FirstKeyTime { get; set; }

        [BsonElement("endTime")]
        public string EndTime { get; set; } = string.Empty;

        [BsonElement("timeToFirstKeyMs")]
        public int TimeToFirstKeyMs { get; set; }

        [BsonElement("totalTypingDurationMs")]
        public int TotalTypingDurationMs { get; set; }

        [BsonElement("activeTypingDurationMs")]
        public int ActiveTypingDurationMs { get; set; }

        [BsonElement("typingSpeedCharsPerSecond")]
        public double TypingSpeedCharsPerSecond { get; set; }

        [BsonElement("typingSpeedWordsPerMinute")]
        public double TypingSpeedWordsPerMinute { get; set; }
    }

    public class TypingSpeedByWordCount
    {
        [BsonElement("wordCount")]
        public int WordCount { get; set; }

        [BsonElement("typingSpeedCharsPerSecond")]
        public double TypingSpeedCharsPerSecond { get; set; }

        [BsonElement("typingSpeedWordsPerMinute")]
        public double TypingSpeedWordsPerMinute { get; set; }

        [BsonElement("timeToFirstKeyMs")]
        public int TimeToFirstKeyMs { get; set; }

        [BsonElement("totalTypingDurationMs")]
        public int TotalTypingDurationMs { get; set; }

        [BsonElement("activeTypingDurationMs")]
        public int ActiveTypingDurationMs { get; set; }
    }
}