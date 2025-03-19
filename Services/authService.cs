using MongoDB.Driver;
using LoftViewer.Models;
using Microsoft.Extensions.Options;
using BCrypt.Net;

namespace LoftViewer.Services;

public abstract class AuthService
{
    private readonly IMongoCollection<UserModel> _usersCollection;

    protected AuthService(IOptions<MongoDBSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.GetConnectionString());
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _usersCollection = mongoDatabase.GetCollection<UserModel>("users");
    }

    public async Task<bool> RegisterUser(string username, string password)
    {
        var existingUser = await _usersCollection.Find(u => u.UserName == username).FirstOrDefaultAsync();
        if (existingUser != null) return false; // User already exists

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var newUser = new UserModel
        {
            UserName = username,
            PasswordHash = hashedPassword
        };

        await _usersCollection.InsertOneAsync(newUser);
        return true;
    }

    public async Task<UserModel?> AuthenticateUser(string username, string password)
    {
        var user = await _usersCollection.Find(u => u.UserName == username).FirstOrDefaultAsync();
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }
}