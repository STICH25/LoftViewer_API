using LoftViewer.Models;

namespace LoftViewer.Data;

public interface IBirdRepository
{
    /// <summary>All birds, without image bytes.</summary>
    Task<IReadOnlyList<Bird>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>One bird, without image bytes.</summary>
    Task<Bird?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<byte[]?> GetImageAsync(string id, CancellationToken cancellationToken);

    Task<Bird?> FindByNameOrNumberAsync(string? birdName, string? birdNumber, CancellationToken cancellationToken);

    Task CreateAsync(Bird bird, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the details of <paramref name="bird"/>. The stored image is only replaced when
    /// <see cref="Bird.ImageBytes"/> is set. Returns false when no bird has that id.
    /// </summary>
    Task<bool> UpdateAsync(Bird bird, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
