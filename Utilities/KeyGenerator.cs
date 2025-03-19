using System.Security.Cryptography;
using System.Text;

namespace LoftViewer.Utilities;

public abstract class KeyGenerator
{
    internal static string GenerateStrongSecret()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var key = new byte[32]; // 256-bit key
            rng.GetBytes(key);
            return Convert.ToBase64String(key);
        }
    }
    
    public static string GetSecret() => GenerateStrongSecret();
}