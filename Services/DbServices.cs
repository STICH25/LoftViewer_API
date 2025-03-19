using LoftViewer.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LoftViewer.Services;

public class DbServices
{
    private readonly MongoClient _mongoClient;
    private readonly IMongoDatabase _database;
    private IMongoCollection<UserModel> _usersCollection;
    private IMongoCollection<Bird> _birdsCollection;

    public DbServices(IOptions<MongoDBSettings> mongoDbSettings)
    {
        _mongoClient = new MongoClient(mongoDbSettings.Value.GetConnectionString());
        _database = _mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _usersCollection = _database.GetCollection<UserModel>("users");
        _birdsCollection = _database.GetCollection<Bird>("Birds");
    }

    // Method to register a new user
    public async Task<bool> RegisterUserAsync(string username, string email, string password)
    {
        var existingUser = await _usersCollection.Find(u => u.UserName == username || u.UserEmail == email).FirstOrDefaultAsync();
        if (existingUser != null) return false;

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var newUser = new UserModel
        {
            UserName = username,
            UserEmail = email,
            PasswordHash = hashedPassword
        };

        await _usersCollection.InsertOneAsync(newUser);
        return true;
    }

    // Method to authenticate the user based on username and password
    public async Task<UserModel?> AuthenticateUserAsync(string username, string password)
    {
        var user = await _usersCollection.Find(u => u.UserName == username).FirstOrDefaultAsync();
        Console.WriteLine($"This is user name: {username}");
        if (user == null || !user.UserName.Equals(username))
        {
            Console.WriteLine($"Wrong username: {username}");
            return null; // Invalid login
        }
        return user;
    }
    
    public async Task<List<Bird>> GetAsync() =>
        await _birdsCollection.Find(_ => true).ToListAsync();

    public async Task<Bird?> GetByIdAsync(string id) =>
        await _birdsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreateAsync(Bird newBird) =>
        await _birdsCollection.InsertOneAsync(newBird);

    public async Task UpdateAsync(string id, Bird updatedBird) =>
        await _birdsCollection.ReplaceOneAsync(x => x.Id == id, updatedBird);

    public async Task DeleteAsync(string id) =>
        await _birdsCollection.DeleteOneAsync(x => x.Id == id);

    public async Task<Bird> FindByNameOrNumberAsync(string? birdName, string? birdNumber)
    {
        return await _birdsCollection
            .Find(b => b.BirdName == birdName || b.BirdNumber == birdNumber)
            .FirstOrDefaultAsync();
    }
}