using LoftViewer.Models;
using MongoDB.Driver;

namespace LoftViewer.Services;

public class UserService
{
    private readonly IMongoCollection<UserModel> _users;

    public UserService(IMongoDatabase database)
    {
        _users = database.GetCollection<UserModel>("users");
    }

    // Register new user
    public async Task<bool> RegisterUser(string userName, string email, string password)
    {
        var existingUser = await _users.Find(u => u.UserEmail == email).FirstOrDefaultAsync();
        if (existingUser != null) return false; // Email already in use

        var newUser = new UserModel
        {
            UserName = userName,
            UserEmail = email,
        };
        newUser.SetPassword(password);

        await _users.InsertOneAsync(newUser);
        return true;
    }

    // Authenticate user
    public async Task<UserModel?> AuthenticateUserAsync(string email, string password)
    {
        var user = await _users.Find(u => u.UserEmail == email).FirstOrDefaultAsync();
        if (user == null || !user.VerifyPassword(password)) 
            return null;

        return user;
    }
}