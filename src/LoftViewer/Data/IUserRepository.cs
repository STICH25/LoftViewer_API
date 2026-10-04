using LoftViewer.Models;

namespace LoftViewer.Data;

public interface IUserRepository
{
    Task<AppUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string userName, string email, CancellationToken cancellationToken);

    Task CreateAsync(AppUser user, CancellationToken cancellationToken);
}
