namespace LoftViewer.Models;

public class AddNewUserModel
{
    public required string UserName { get; set; }
    public string? Email { get; set; }
    public required string Password { get; set; }
}