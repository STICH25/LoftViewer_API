using System.Collections.Concurrent;
using LoftViewer.Data;
using LoftViewer.Models;
using MongoDB.Bson;

namespace LoftViewer.Tests.Infrastructure;

public sealed class InMemoryBirdRepository : IBirdRepository
{
    public ConcurrentDictionary<string, Bird> Birds { get; } = new();

    public Bird Add(Bird bird)
    {
        if (string.IsNullOrEmpty(bird.Id))
        {
            bird.Id = ObjectId.GenerateNewId().ToString();
        }

        Birds[bird.Id] = bird;
        return bird;
    }

    public Task<IReadOnlyList<Bird>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Bird>>(Birds.Values.Select(WithoutImage).ToList());

    public Task<Bird?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(Birds.TryGetValue(id, out var bird) ? WithoutImage(bird) : null);

    public Task<byte[]?> GetImageAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(Birds.TryGetValue(id, out var bird) ? bird.ImageBytes : null);

    public Task<Bird?> FindByNameOrNumberAsync(string? birdName, string? birdNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Birds.Values.FirstOrDefault(b => b.BirdName == birdName || b.BirdNumber == birdNumber));

    public Task CreateAsync(Bird bird, CancellationToken cancellationToken)
    {
        Add(bird);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Bird bird, CancellationToken cancellationToken)
    {
        if (!Birds.TryGetValue(bird.Id, out var existing))
        {
            return Task.FromResult(false);
        }

        bird.ImageBytes ??= existing.ImageBytes;
        Birds[bird.Id] = bird;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(Birds.TryRemove(id, out _));

    private static Bird WithoutImage(Bird b) => new()
    {
        Id = b.Id,
        BirdName = b.BirdName,
        BirdNumber = b.BirdNumber,
        BirdColor = b.BirdColor,
        BirdFather = b.BirdFather,
        BirdMother = b.BirdMother,
        Champion = b.Champion,
    };
}

public sealed class InMemoryUserRepository : IUserRepository
{
    public ConcurrentDictionary<string, AppUser> Users { get; } = new();

    public AppUser Add(string userName, string password, string role)
    {
        var user = new AppUser
        {
            UserId = ObjectId.GenerateNewId().ToString(),
            UserName = userName,
            UserEmail = $"{userName}@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
        };
        Users[userName] = user;
        return user;
    }

    public Task<AppUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        Task.FromResult(Users.TryGetValue(userName, out var user) ? user : null);

    public Task<bool> ExistsAsync(string userName, string email, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Values.Any(u => u.UserName == userName || u.UserEmail == email));

    public Task CreateAsync(AppUser user, CancellationToken cancellationToken)
    {
        user.UserId = ObjectId.GenerateNewId().ToString();
        Users[user.UserName] = user;
        return Task.CompletedTask;
    }
}
