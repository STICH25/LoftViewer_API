using MongoDB.Driver;

namespace LoftViewer.Models;

public class MongoDBSettings
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public MongoClientSettings ConnectionString { get; set; }

    public string GetConnectionString()
    {
        return $"mongodb+srv://{Username}:{Password}@{Host}/?retryWrites=true&w=majority&appName={AppName}";
    }
}