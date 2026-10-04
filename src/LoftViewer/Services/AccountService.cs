using LoftViewer.Data;
using LoftViewer.Models;

namespace LoftViewer.Services;

public enum RegistrationResult
{
    Created,
    AlreadyExists,
}

public interface IAccountService
{
    /// <summary>Returns the user when the credentials match, otherwise null.</summary>
    Task<AppUser?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);

    Task<RegistrationResult> RegisterAsync(string userName, string email, string password, CancellationToken cancellationToken);
}

public sealed class AccountService(IUserRepository users) : IAccountService
{
    // Verified against when the user does not exist, so an unknown user name takes as long to
    // reject as a wrong password and response timing does not reveal which accounts exist.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    public async Task<AppUser?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByUserNameAsync(userName, cancellationToken);
        var passwordMatches = BCrypt.Net.BCrypt.Verify(password, user?.PasswordHash ?? DummyHash);
        return passwordMatches ? user : null;
    }

    public async Task<RegistrationResult> RegisterAsync(string userName, string email, string password, CancellationToken cancellationToken)
    {
        if (await users.ExistsAsync(userName, email, cancellationToken))
        {
            return RegistrationResult.AlreadyExists;
        }

        await users.CreateAsync(new AppUser
        {
            UserName = userName,
            UserEmail = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.User,
        }, cancellationToken);

        return RegistrationResult.Created;
    }
}
