using System.Security.Cryptography;
using System.Text;

public static class ScenarioSourceHash
{
    public static string Compute(string sourceText)
    {
        string normalized = sourceText ?? string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(normalized);

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    public static bool IsStale(ScenarioSourceMetadata metadata, string currentSourceText)
    {
        if (metadata == null)
        {
            return true;
        }

        return metadata.SourceHash != Compute(currentSourceText);
    }
}
