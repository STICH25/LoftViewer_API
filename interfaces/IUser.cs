namespace LoftViewer.interfaces;

public interface IUser
{
    string UserId { get; set; }
    string UserName { get; set; }
    string PasswordHash { get; set; }
    string? UserEmail { get; set; }
    string Role { get; set; }
}