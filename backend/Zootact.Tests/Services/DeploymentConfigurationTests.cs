using System.Text;
using Microsoft.Extensions.Configuration;
using Zootact.API.Services;

namespace Zootact.Tests.Services;

public sealed class DeploymentConfigurationTests
{
    [Fact]
    public void ResolveAllowedOrigins_UsesExplicitAllowedOriginsList()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:AllowedOrigins"] = "https://app.example.com, https://staging.example.com "
            })
            .Build();

        var origins = FrontendOriginResolver.Resolve(configuration);

        Assert.Equal(["https://app.example.com", "https://staging.example.com"], origins);
    }

    [Fact]
    public void ResolveAllowedOrigins_FallsBackToLegacyFrontendUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Url"] = "https://app.example.com"
            })
            .Build();

        var origins = FrontendOriginResolver.Resolve(configuration);

        Assert.Equal(["https://app.example.com"], origins);
    }

    [Fact]
    public void LoadFirebaseAdminJson_PrefersInlineJson()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:ServiceAccountJson"] = "{\"project_id\":\"inline\"}",
                ["Firebase:ServiceAccountJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"project_id\":\"base64\"}")),
                ["Firebase:ServiceAccountKeyPath"] = "Config/firebase-adminsdk.json"
            })
            .Build();

        var resolved = FirebaseAdminCredentialLoader.Load(configuration, Path.GetTempPath());

        Assert.Equal("{\"project_id\":\"inline\"}", resolved.Json);
        Assert.Equal("inline-json", resolved.Source);
    }

    [Fact]
    public void LoadFirebaseAdminJson_FallsBackToBase64()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"project_id\":\"base64\"}"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:ServiceAccountJsonBase64"] = encoded
            })
            .Build();

        var resolved = FirebaseAdminCredentialLoader.Load(configuration, Path.GetTempPath());

        Assert.Equal("{\"project_id\":\"base64\"}", resolved.Json);
        Assert.Equal("base64-json", resolved.Source);
    }

    [Fact]
    public void LoadFirebaseAdminJson_FallsBackToFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"zootact-firebase-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var configDir = Path.Combine(tempDir, "Config");
            Directory.CreateDirectory(configDir);
            var keyPath = Path.Combine(configDir, "firebase-adminsdk.json");
            File.WriteAllText(keyPath, "{\"project_id\":\"file\"}");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Firebase:ServiceAccountKeyPath"] = "Config/firebase-adminsdk.json"
                })
                .Build();

            var resolved = FirebaseAdminCredentialLoader.Load(configuration, tempDir);

            Assert.Equal("{\"project_id\":\"file\"}", resolved.Json);
            Assert.Equal("file", resolved.Source);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
