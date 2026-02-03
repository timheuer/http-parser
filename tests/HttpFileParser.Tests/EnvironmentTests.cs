using HttpFileParser.Environment;

namespace HttpFileParser.Tests;

public class EnvironmentTests
{
    [Fact]
    public void EnvironmentFileParser_ParsesEnvironments()
    {
        var json = """
            {
              "development": {
                "baseUrl": "http://localhost:3000",
                "apiKey": "dev-key"
              },
              "production": {
                "baseUrl": "https://api.example.com",
                "apiKey": "prod-key"
              }
            }
            """;

        var parser = new EnvironmentFileParser();
        var envFile = parser.Parse(json);

        Assert.Equal(2, envFile.EnvironmentNames.Count());
        Assert.Contains("development", envFile.EnvironmentNames);
        Assert.Contains("production", envFile.EnvironmentNames);
    }

    [Fact]
    public void EnvironmentFileParser_ParsesSharedVariables()
    {
        var json = """
            {
              "$shared": {
                "version": "v1"
              },
              "development": {
                "baseUrl": "http://localhost:3000"
              }
            }
            """;

        var parser = new EnvironmentFileParser();
        var envFile = parser.Parse(json);

        Assert.NotNull(envFile.SharedVariables);
        Assert.Equal("v1", envFile.SharedVariables["version"]);
    }

    [Fact]
    public void EnvironmentFile_GetMergedEnvironment_MergesSharedAndEnv()
    {
        var json = """
            {
              "$shared": {
                "version": "v1",
                "timeout": "30"
              },
              "development": {
                "baseUrl": "http://localhost:3000",
                "timeout": "60"
              }
            }
            """;

        var parser = new EnvironmentFileParser();
        var envFile = parser.Parse(json);

        var merged = envFile.GetMergedEnvironment("development");

        Assert.Equal("v1", merged["version"]);
        Assert.Equal("http://localhost:3000", merged["baseUrl"]);
        Assert.Equal("60", merged["timeout"]); // Environment overrides shared
    }

    [Fact]
    public void EnvironmentSelector_SelectsEnvironment()
    {
        var json = """
            {
              "development": {
                "baseUrl": "http://localhost:3000"
              },
              "production": {
                "baseUrl": "https://api.example.com"
              }
            }
            """;

        var parser = new EnvironmentFileParser();
        var envFile = parser.Parse(json);

        var selector = new EnvironmentSelector();
        selector.AddEnvironmentFile(envFile);
        selector.SelectEnvironment("production");

        var variables = selector.GetMergedVariables();

        Assert.Equal("https://api.example.com", variables["baseUrl"]);
    }

    [Fact]
    public void EnvironmentFileParser_ParsesVsCodeSettings()
    {
        var json = """
            {
              "rest-client.environmentVariables": {
                "development": {
                  "baseUrl": "http://localhost:3000"
                },
                "production": {
                  "baseUrl": "https://api.example.com"
                }
              }
            }
            """;

        var parser = new EnvironmentFileParser();
        var envFile = parser.ParseVsCodeSettings(json);

        Assert.Equal(2, envFile.EnvironmentNames.Count());
    }

    [Fact]
    public void EnvironmentSelector_CreateContext_CreatesWorkingContext()
    {
        var json = """
            {
              "test": {
                "apiKey": "test-key"
              }
            }
            """;

        var parser = new EnvironmentFileParser();
        var envFile = parser.Parse(json);

        var selector = new EnvironmentSelector();
        selector.AddEnvironmentFile(envFile);
        selector.SelectEnvironment("test");

        var context = selector.CreateContext();

        Assert.Equal("test-key", context.Resolve("apiKey"));
    }
}
