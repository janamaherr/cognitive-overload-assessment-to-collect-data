using MongoDB.Driver;

namespace CognitiveOverloadLMS.Services
{
    public class MongoDBService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoClient _client;
        
        public MongoDBService(IConfiguration configuration)
        {
            try
            {
                var connectionString = configuration.GetSection("MongoDBSettings:ConnectionString").Value;
                var databaseName = configuration.GetSection("MongoDBSettings:DatabaseName").Value;
                
                Console.WriteLine($"Connecting to MongoDB: {connectionString?.Replace(GetPassword(connectionString), "****")}");

                // Use direct client initialization to avoid eager SRV/TXT DNS resolution on startup.
                _client = new MongoClient(connectionString);
                _database = _client.GetDatabase(databaseName);
                
                Console.WriteLine("MongoDB client initialized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to MongoDB: {ex.Message}");
                throw;
            }
        }
        
        private string GetPassword(string connectionString)
        {
            // Extract password from connection string for logging (to hide it)
            try
            {
                var start = connectionString.IndexOf("://") + 3;
                var atIndex = connectionString.IndexOf("@");
                if (start > 2 && atIndex > start)
                {
                    var credentials = connectionString.Substring(start, atIndex - start);
                    var colonIndex = credentials.IndexOf(":");
                    if (colonIndex > 0)
                    {
                        return credentials.Substring(colonIndex);
                    }
                }
            }
            catch { }
            return "";
        }
        
        public IMongoDatabase GetDatabase() => _database;
        
        public IMongoCollection<T> GetCollection<T>(string name)
        {
            return _database.GetCollection<T>(name);
        }
    }
}