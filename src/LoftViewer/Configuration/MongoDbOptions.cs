using System.ComponentModel.DataAnnotations;

namespace LoftViewer.Configuration;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDBSettings";

    /// <summary>A complete connection string. When set it takes precedence over the Atlas parts below.</summary>
    public string? ConnectionString { get; set; }

    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;

    [Required]
    public string DatabaseName { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString)
        || (!string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password));

    public string BuildConnectionString() =>
        !string.IsNullOrWhiteSpace(ConnectionString)
            ? ConnectionString
            // Credentials must be percent-encoded or a password containing '@', ':' or '/' breaks the URI.
            : $"mongodb+srv://{Uri.EscapeDataString(Username)}:{Uri.EscapeDataString(Password)}@{Host}/"
              + $"?retryWrites=true&w=majority&appName={Uri.EscapeDataString(AppName)}";
}
