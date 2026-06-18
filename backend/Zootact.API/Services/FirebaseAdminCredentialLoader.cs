using System.Text;

namespace Zootact.API.Services;

public sealed record FirebaseAdminCredentialSource(string Json, string Source);

public static class FirebaseAdminCredentialLoader
{
    public static FirebaseAdminCredentialSource Load(IConfiguration configuration, string contentRootPath)
    {
        var inlineJson = configuration["Firebase:ServiceAccountJson"];
        if (!string.IsNullOrWhiteSpace(inlineJson))
        {
            return new FirebaseAdminCredentialSource(inlineJson, "inline-json");
        }

        var base64Json = configuration["Firebase:ServiceAccountJsonBase64"];
        if (!string.IsNullOrWhiteSpace(base64Json))
        {
            var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(base64Json));
            return new FirebaseAdminCredentialSource(decodedJson, "base64-json");
        }

        var relativeOrAbsolutePath = configuration["Firebase:ServiceAccountKeyPath"] ?? "Config/firebase-adminsdk.json";
        var resolvedPath = Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.Combine(contentRootPath, relativeOrAbsolutePath);

        if (!File.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Firebase service account secret was not configured. Expected env Firebase__ServiceAccountJson / Firebase__ServiceAccountJsonBase64 or file at {resolvedPath}");
        }

        return new FirebaseAdminCredentialSource(File.ReadAllText(resolvedPath), "file");
    }
}
