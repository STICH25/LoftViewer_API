using LoftViewer.Models;
using MongoDB.Driver;

namespace LoftViewer.Data;

public sealed class UserRepository(IMongoDatabase database) : IUserRepository
{
    public const string CollectionName = "users";

    private readonly IMongoCollection<AppUser> _users = database.GetCollection<AppUser>(CollectionName);

    public async Task<AppUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        await _users.Find(u => u.UserName == userName).FirstOrDefaultAsync(cancellationToken);

    public async Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        await _users.Find(u => u.UserId == userId).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(string userName, string email, CancellationToken cancellationToken) =>
        _users.Find(u => u.UserName == userName || u.UserEmail == email).AnyAsync(cancellationToken);

    public Task CreateAsync(AppUser user, CancellationToken cancellationToken) =>
        _users.InsertOneAsync(user, cancellationToken: cancellationToken);
}
