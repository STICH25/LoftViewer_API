using LoftViewer.Models;
using MongoDB.Driver;

namespace LoftViewer.Data;

public sealed class BirdRepository(IMongoDatabase database) : IBirdRepository
{
    public const string CollectionName = "Birds";

    // Images are stored inline and can be megabytes each; never pull them for list/detail reads.
    private static readonly ProjectionDefinition<Bird> WithoutImage =
        Builders<Bird>.Projection.Exclude(b => b.ImageBytes);

    private readonly IMongoCollection<Bird> _birds = database.GetCollection<Bird>(CollectionName);

    public async Task<IReadOnlyList<Bird>> GetAllAsync(CancellationToken cancellationToken) =>
        await _birds.Find(FilterDefinition<Bird>.Empty)
            .Project<Bird>(WithoutImage)
            .ToListAsync(cancellationToken);

    public async Task<Bird?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _birds.Find(b => b.Id == id)
            .Project<Bird>(WithoutImage)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<byte[]?> GetImageAsync(string id, CancellationToken cancellationToken) =>
        await _birds.Find(b => b.Id == id)
            .Project(b => b.ImageBytes)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Bird?> FindByNameOrNumberAsync(string? birdName, string? birdNumber, CancellationToken cancellationToken) =>
        await _birds.Find(b => b.BirdName == birdName || b.BirdNumber == birdNumber)
            .Project<Bird>(WithoutImage)
            .FirstOrDefaultAsync(cancellationToken);

    public Task CreateAsync(Bird bird, CancellationToken cancellationToken) =>
        _birds.InsertOneAsync(bird, cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(Bird bird, CancellationToken cancellationToken)
    {
        var update = Builders<Bird>.Update
            .Set(b => b.BirdName, bird.BirdName)
            .Set(b => b.BirdNumber, bird.BirdNumber)
            .Set(b => b.BirdColor, bird.BirdColor)
            .Set(b => b.BirdFather, bird.BirdFather)
            .Set(b => b.BirdMother, bird.BirdMother)
            .Set(b => b.Champion, bird.Champion);

        if (bird.ImageBytes is not null)
        {
            update = update.Set(b => b.ImageBytes, bird.ImageBytes);
        }

        var result = await _birds.UpdateOneAsync(b => b.Id == bird.Id, update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _birds.DeleteOneAsync(b => b.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }
}
