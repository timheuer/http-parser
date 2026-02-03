using HttpFileParser.Environment;
using HttpFileParser.Variables;

namespace HttpFileParser.Tests;

public class EnvironmentTests : IDisposable
{
  private readonly List<string> _tempDirectories = [];

  public void Dispose()
  {
    foreach (var dir in _tempDirectories)
    {
      try
      {
        if (Directory.Exists(dir))
        {
          Directory.Delete(dir, recursive: true);
        }
      }
      catch
      {
        // Ignore cleanup errors
      }
    }
    GC.SuppressFinalize(this);
  }

  private string CreateTempDirectory()
  {
    var path = Path.Combine(Path.GetTempPath(), "HttpFileParserTests_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    _tempDirectories.Add(path);
    return path;
  }

  #region EnvironmentDiscovery Tests

  [Fact]
  public void EnvironmentDiscovery_DiscoverAndLoad_FindsHttpClientEnvJson()
  {
    var tempDir = CreateTempDirectory();
    var envContent = """
            {
              "development": {
                "baseUrl": "http://localhost:3000"
              }
            }
            """;
    File.WriteAllText(Path.Combine(tempDir, "http-client.env.json"), envContent);

    var discovery = new EnvironmentDiscovery();
    var selector = discovery.DiscoverAndLoad(tempDir);

    Assert.Contains("development", selector.AvailableEnvironments);
  }

  [Fact]
  public void EnvironmentDiscovery_DiscoverAndLoad_FindsUserEnvFile()
  {
    var tempDir = CreateTempDirectory();
    var userEnvContent = """
            {
              "local": {
                "userSecret": "my-secret"
              }
            }
            """;
    File.WriteAllText(Path.Combine(tempDir, "http-client.env.json.user"), userEnvContent);

    var discovery = new EnvironmentDiscovery();
    var selector = discovery.DiscoverAndLoad(tempDir);

    Assert.Contains("local", selector.AvailableEnvironments);
    selector.SelectEnvironment("local");
    var vars = selector.GetMergedVariables();
    Assert.Equal("my-secret", vars["userSecret"]);
  }

  [Fact]
  public void EnvironmentDiscovery_DiscoverAndLoad_FindsPrivateEnvFile()
  {
    var tempDir = CreateTempDirectory();
    var privateEnvContent = """
            {
              "secrets": {
                "apiToken": "private-token"
              }
            }
            """;
    File.WriteAllText(Path.Combine(tempDir, "http-client.private.env.json"), privateEnvContent);

    var discovery = new EnvironmentDiscovery();
    var selector = discovery.DiscoverAndLoad(tempDir);

    Assert.Contains("secrets", selector.AvailableEnvironments);
    selector.SelectEnvironment("secrets");
    var vars = selector.GetMergedVariables();
    Assert.Equal("private-token", vars["apiToken"]);
  }

  [Fact]
  public void EnvironmentDiscovery_DiscoverAndLoad_FindsVsCodeSettings()
  {
    var tempDir = CreateTempDirectory();
    var vsCodeDir = Path.Combine(tempDir, ".vscode");
    Directory.CreateDirectory(vsCodeDir);
    var settingsContent = """
            {
              "rest-client.environmentVariables": {
                "vscode-env": {
                  "fromVsCode": "true"
                }
              }
            }
            """;
    File.WriteAllText(Path.Combine(vsCodeDir, "settings.json"), settingsContent);

    var discovery = new EnvironmentDiscovery();
    var selector = discovery.DiscoverAndLoad(tempDir);

    Assert.Contains("vscode-env", selector.AvailableEnvironments);
    selector.SelectEnvironment("vscode-env");
    var vars = selector.GetMergedVariables();
    Assert.Equal("true", vars["fromVsCode"]);
  }

  [Fact]
  public void EnvironmentDiscovery_DiscoverAndLoad_WithEmptyDirectory_ReturnsEmptySelector()
  {
    var tempDir = CreateTempDirectory();

    var discovery = new EnvironmentDiscovery();
    var selector = discovery.DiscoverAndLoad(tempDir);

    Assert.Empty(selector.AvailableEnvironments);
    Assert.Empty(selector.GetMergedVariables());
  }

  [Fact]
  public void EnvironmentDiscovery_FindEnvironmentFiles_ReturnsAllFoundFiles()
  {
    var tempDir = CreateTempDirectory();

    // Create all possible environment files
    File.WriteAllText(Path.Combine(tempDir, "http-client.env.json"), "{}");
    File.WriteAllText(Path.Combine(tempDir, "http-client.env.json.user"), "{}");
    File.WriteAllText(Path.Combine(tempDir, "http-client.private.env.json"), "{}");

    var vsCodeDir = Path.Combine(tempDir, ".vscode");
    Directory.CreateDirectory(vsCodeDir);
    File.WriteAllText(Path.Combine(vsCodeDir, "settings.json"), "{}");

    var files = EnvironmentDiscovery.FindEnvironmentFiles(tempDir).ToList();

    Assert.Equal(4, files.Count);
    Assert.Contains(files, f => f.EndsWith("http-client.env.json"));
    Assert.Contains(files, f => f.EndsWith("http-client.env.json.user"));
    Assert.Contains(files, f => f.EndsWith("http-client.private.env.json"));
    Assert.Contains(files, f => f.EndsWith("settings.json"));
  }

  [Fact]
  public void EnvironmentDiscovery_FindEnvironmentFiles_WithNoFiles_ReturnsEmpty()
  {
    var tempDir = CreateTempDirectory();

    var files = EnvironmentDiscovery.FindEnvironmentFiles(tempDir).ToList();

    Assert.Empty(files);
  }

  [Fact]
  public void EnvironmentDiscovery_DiscoverAndLoad_MergesMultipleFiles()
  {
    var tempDir = CreateTempDirectory();

    // Base env file
    File.WriteAllText(Path.Combine(tempDir, "http-client.env.json"), """
            {
              "dev": {
                "baseUrl": "http://localhost",
                "timeout": "30"
              }
            }
            """);

    // User file overrides
    File.WriteAllText(Path.Combine(tempDir, "http-client.env.json.user"), """
            {
              "dev": {
                "timeout": "60",
                "userSpecific": "value"
              }
            }
            """);

    var discovery = new EnvironmentDiscovery();
    var selector = discovery.DiscoverAndLoad(tempDir);
    selector.SelectEnvironment("dev");
    var vars = selector.GetMergedVariables();

    Assert.Equal("http://localhost", vars["baseUrl"]);
    Assert.Equal("60", vars["timeout"]); // User overrides base
    Assert.Equal("value", vars["userSpecific"]);
  }

  #endregion

  #region EnvironmentSelector Tests

  [Fact]
  public void EnvironmentSelector_SelectedEnvironment_ReturnsCurrentSelection()
  {
    var selector = new EnvironmentSelector();

    Assert.Null(selector.SelectedEnvironment);

    selector.SelectEnvironment("production");
    Assert.Equal("production", selector.SelectedEnvironment);

    selector.SelectEnvironment("development");
    Assert.Equal("development", selector.SelectedEnvironment);

    selector.SelectEnvironment(null);
    Assert.Null(selector.SelectedEnvironment);
  }

  [Fact]
  public void EnvironmentSelector_AvailableEnvironments_ReturnsAllEnvironmentNames()
  {
    var parser = new EnvironmentFileParser();
    var file1 = parser.Parse("""
            {
              "dev": { "key": "value" },
              "staging": { "key": "value" }
            }
            """);
    var file2 = parser.Parse("""
            {
              "prod": { "key": "value" },
              "dev": { "key": "different" }
            }
            """);

    var selector = new EnvironmentSelector();
    selector.AddEnvironmentFile(file1);
    selector.AddEnvironmentFile(file2);

    var environments = selector.AvailableEnvironments.ToList();

    Assert.Equal(3, environments.Count);
    Assert.Contains("dev", environments);
    Assert.Contains("staging", environments);
    Assert.Contains("prod", environments);
  }

  [Fact]
  public void EnvironmentSelector_MultipleFiles_MergeCorrectly()
  {
    var parser = new EnvironmentFileParser();
    var file1 = parser.Parse("""
            {
              "$shared": { "common": "from-file1" },
              "dev": { "url": "localhost" }
            }
            """);
    var file2 = parser.Parse("""
            {
              "$shared": { "extra": "from-file2" },
              "dev": { "port": "3000" }
            }
            """);

    var selector = new EnvironmentSelector();
    selector.AddEnvironmentFile(file1);
    selector.AddEnvironmentFile(file2);
    selector.SelectEnvironment("dev");

    var vars = selector.GetMergedVariables();

    Assert.Equal("from-file1", vars["common"]);
    Assert.Equal("from-file2", vars["extra"]);
    Assert.Equal("localhost", vars["url"]);
    Assert.Equal("3000", vars["port"]);
  }

  [Fact]
  public void EnvironmentSelector_LaterFileOverridesEarlier()
  {
    var parser = new EnvironmentFileParser();
    var file1 = parser.Parse("""
            {
              "dev": {
                "apiKey": "base-key",
                "baseUrl": "http://localhost"
              }
            }
            """);
    var file2 = parser.Parse("""
            {
              "dev": {
                "apiKey": "override-key"
              }
            }
            """);

    var selector = new EnvironmentSelector();
    selector.AddEnvironmentFile(file1);
    selector.AddEnvironmentFile(file2);
    selector.SelectEnvironment("dev");

    var vars = selector.GetMergedVariables();

    Assert.Equal("override-key", vars["apiKey"]); // Later file wins
    Assert.Equal("http://localhost", vars["baseUrl"]); // Original preserved
  }

  [Fact]
  public void EnvironmentSelector_CreateResolver_CreatesWorkingResolver()
  {
    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse("""
            {
              "test": {
                "hostname": "api.test.com",
                "port": "8080"
              }
            }
            """);

    var selector = new EnvironmentSelector();
    selector.AddEnvironmentFile(envFile);
    selector.SelectEnvironment("test");

    var resolver = selector.CreateResolver();

    Assert.True(resolver.CanResolve("hostname"));
    Assert.Equal("api.test.com", resolver.Resolve("hostname"));
    Assert.True(resolver.CanResolve("port"));
    Assert.Equal("8080", resolver.Resolve("port"));
    Assert.False(resolver.CanResolve("nonexistent"));
  }

  [Fact]
  public void EnvironmentSelector_SelectEnvironment_WithNonExistent_ReturnsEmptyVariables()
  {
    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse("""
            {
              "existing": {
                "key": "value"
              }
            }
            """);

    var selector = new EnvironmentSelector();
    selector.AddEnvironmentFile(envFile);
    selector.SelectEnvironment("nonexistent");

    var vars = selector.GetMergedVariables();

    Assert.Empty(vars);
  }

  [Fact]
  public void EnvironmentSelector_GetMergedVariables_WithNoEnvironmentSelected_ReturnsOnlyShared()
  {
    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse("""
            {
              "$shared": {
                "sharedKey": "sharedValue"
              },
              "dev": {
                "envKey": "envValue"
              }
            }
            """);

    var selector = new EnvironmentSelector();
    selector.AddEnvironmentFile(envFile);
    // No environment selected

    var vars = selector.GetMergedVariables();

    Assert.Single(vars);
    Assert.Equal("sharedValue", vars["sharedKey"]);
  }

  [Fact]
  public void EnvironmentSelector_CreateContext_IncludesDynamicResolver()
  {
    var selector = new EnvironmentSelector();
    var context = selector.CreateContext();

    // Dynamic resolver should handle $guid
    var guid = context.Resolve("$guid");
    Assert.NotNull(guid);
    Assert.True(Guid.TryParse(guid, out _));
  }

  #endregion

  #region EnvironmentFileParser Edge Cases

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
  public void EnvironmentFileParser_ParseNumberValues_ConvertsToString()
  {
    var json = """
            {
              "test": {
                "intValue": 42,
                "floatValue": 3.14,
                "negativeValue": -100,
                "scientificValue": 1.5e10
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);
    var env = envFile.GetEnvironment("test");

    Assert.NotNull(env);
    Assert.Equal("42", env["intValue"]);
    Assert.Equal("3.14", env["floatValue"]);
    Assert.Equal("-100", env["negativeValue"]);
    Assert.Equal("1.5e10", env["scientificValue"]);
  }

  [Fact]
  public void EnvironmentFileParser_ParseBooleanValues_ConvertsToString()
  {
    var json = """
            {
              "test": {
                "enabled": true,
                "disabled": false
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);
    var env = envFile.GetEnvironment("test");

    Assert.NotNull(env);
    Assert.Equal("true", env["enabled"]);
    Assert.Equal("false", env["disabled"]);
  }

  [Fact]
  public void EnvironmentFileParser_ParseNullValues_AreSkipped()
  {
    var json = """
            {
              "test": {
                "validKey": "value",
                "nullKey": null
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);
    var env = envFile.GetEnvironment("test");

    Assert.NotNull(env);
    Assert.Single(env);
    Assert.Equal("value", env["validKey"]);
    Assert.False(env.ContainsKey("nullKey"));
  }

  [Fact]
  public void EnvironmentFileParser_ParseInvalidJson_ReturnsEmptyEnvironment()
  {
    var invalidJson = "{ this is not valid json }";

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(invalidJson);

    Assert.Empty(envFile.Environments);
    Assert.Empty(envFile.EnvironmentNames);
    Assert.Null(envFile.SharedVariables);
  }

  [Fact]
  public void EnvironmentFileParser_ParseFile_WithNonExistentFile_ReturnsEmptyEnvironment()
  {
    var parser = new EnvironmentFileParser();
    var envFile = parser.ParseFile("/nonexistent/path/file.json");

    Assert.Empty(envFile.Environments);
    Assert.Empty(envFile.EnvironmentNames);
    Assert.Null(envFile.SharedVariables);
    Assert.Equal("/nonexistent/path/file.json", envFile.FilePath);
  }

  [Fact]
  public void EnvironmentFileParser_ParseFile_WithExistingFile_ParsesCorrectly()
  {
    var tempDir = CreateTempDirectory();
    var filePath = Path.Combine(tempDir, "test.env.json");
    File.WriteAllText(filePath, """
            {
              "test": { "key": "value" }
            }
            """);

    var parser = new EnvironmentFileParser();
    var envFile = parser.ParseFile(filePath);

    Assert.Single(envFile.Environments);
    Assert.Equal(filePath, envFile.FilePath);
  }

  [Fact]
  public void EnvironmentFileParser_ParseVsCodeSettings_WithSharedVariables()
  {
    var json = """
            {
              "rest-client.environmentVariables": {
                "$shared": {
                  "commonVar": "common-value"
                },
                "development": {
                  "envVar": "dev-value"
                }
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.ParseVsCodeSettings(json);

    Assert.NotNull(envFile.SharedVariables);
    Assert.Equal("common-value", envFile.SharedVariables["commonVar"]);
    Assert.Single(envFile.EnvironmentNames);
    Assert.Contains("development", envFile.EnvironmentNames);
  }

  [Fact]
  public void EnvironmentFileParser_ParseVsCodeSettings_WithoutRestClientSection_ReturnsEmpty()
  {
    var json = """
            {
              "editor.fontSize": 14,
              "editor.tabSize": 2
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.ParseVsCodeSettings(json);

    Assert.Empty(envFile.Environments);
    Assert.Empty(envFile.EnvironmentNames);
  }

  [Fact]
  public void EnvironmentFileParser_ParseVsCodeSettings_InvalidJson_ReturnsEmpty()
  {
    var parser = new EnvironmentFileParser();
    var envFile = parser.ParseVsCodeSettings("not valid json");

    Assert.Empty(envFile.Environments);
  }

  [Fact]
  public void EnvironmentFileParser_Parse_PreservesFilePath()
  {
    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse("{}", "my-file.json");

    Assert.Equal("my-file.json", envFile.FilePath);
  }

  [Fact]
  public void EnvironmentFileParser_Parse_ArrayValueInEnvironment_UsesRawText()
  {
    var json = """
            {
              "test": {
                "arrayValue": [1, 2, 3]
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);
    var env = envFile.GetEnvironment("test");

    Assert.NotNull(env);
    Assert.Equal("[1, 2, 3]", env["arrayValue"]);
  }

  [Fact]
  public void EnvironmentFileParser_Parse_ObjectValueInEnvironment_UsesRawText()
  {
    var json = """
            {
              "test": {
                "objectValue": {"nested": "value"}
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);
    var env = envFile.GetEnvironment("test");

    Assert.NotNull(env);
    Assert.Contains("nested", env["objectValue"]);
  }

  #endregion

  #region EnvironmentFile Tests

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
  public void EnvironmentFile_EnvironmentNames_ReturnsAllNamesExceptShared()
  {
    var json = """
            {
              "$shared": { "key": "value" },
              "dev": { "key": "value" },
              "staging": { "key": "value" },
              "prod": { "key": "value" }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);

    var names = envFile.EnvironmentNames.ToList();

    Assert.Equal(3, names.Count);
    Assert.Contains("dev", names);
    Assert.Contains("staging", names);
    Assert.Contains("prod", names);
    Assert.DoesNotContain("$shared", names);
  }

  [Fact]
  public void EnvironmentFile_GetEnvironment_ReturnsVariablesForEnvironment()
  {
    var json = """
            {
              "myenv": {
                "var1": "value1",
                "var2": "value2"
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);

    var env = envFile.GetEnvironment("myenv");

    Assert.NotNull(env);
    Assert.Equal(2, env.Count);
    Assert.Equal("value1", env["var1"]);
    Assert.Equal("value2", env["var2"]);
  }

  [Fact]
  public void EnvironmentFile_GetEnvironment_NonExistent_ReturnsNull()
  {
    var json = """
            {
              "existing": { "key": "value" }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);

    var env = envFile.GetEnvironment("nonexistent");

    Assert.Null(env);
  }

  [Fact]
  public void EnvironmentFile_GetMergedEnvironment_NonExistent_ReturnsOnlyShared()
  {
    var json = """
            {
              "$shared": { "sharedKey": "sharedValue" },
              "existing": { "key": "value" }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);

    var merged = envFile.GetMergedEnvironment("nonexistent");

    Assert.Single(merged);
    Assert.Equal("sharedValue", merged["sharedKey"]);
  }

  [Fact]
  public void EnvironmentFile_GetMergedEnvironment_NoShared_ReturnsOnlyEnv()
  {
    var json = """
            {
              "myenv": {
                "key1": "value1",
                "key2": "value2"
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);

    var merged = envFile.GetMergedEnvironment("myenv");

    Assert.Equal(2, merged.Count);
    Assert.Equal("value1", merged["key1"]);
    Assert.Equal("value2", merged["key2"]);
  }

  [Fact]
  public void EnvironmentFile_GetEnvironment_IsCaseInsensitive()
  {
    var json = """
            {
              "Development": {
                "baseUrl": "http://localhost"
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);

    // Should work with different cases
    Assert.NotNull(envFile.GetEnvironment("Development"));
    Assert.NotNull(envFile.GetEnvironment("development"));
    Assert.NotNull(envFile.GetEnvironment("DEVELOPMENT"));
  }

  [Fact]
  public void EnvironmentFile_Variables_AreCaseInsensitive()
  {
    var json = """
            {
              "test": {
                "BaseUrl": "http://localhost"
              }
            }
            """;

    var parser = new EnvironmentFileParser();
    var envFile = parser.Parse(json);
    var env = envFile.GetEnvironment("test");

    Assert.NotNull(env);
    Assert.True(env.ContainsKey("BaseUrl"));
    Assert.True(env.ContainsKey("baseurl"));
    Assert.True(env.ContainsKey("BASEURL"));
  }

  #endregion

  #region Original Tests (kept for compatibility)

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

  #endregion
}
