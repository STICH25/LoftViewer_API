using System.Text.RegularExpressions;

namespace LoftViewer.Utilities;

public static partial class CredentialRules
{
    public const string PasswordRequirements =
        "Password must be 8-16 characters and include an uppercase letter, a digit and a special character.";

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && EmailPattern().IsMatch(email);

    public static bool IsValidPassword(string? password) =>
        !string.IsNullOrEmpty(password) && PasswordPattern().IsMatch(password);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailPattern();

    // 8-16 characters with at least one uppercase letter, one digit and one non-alphanumeric character.
    [GeneratedRegex(@"^(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,16}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex PasswordPattern();
}
