using HttpFileParser.Environment;
using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Tests;

public class HttpFileTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly List<string> _tempFiles = [];

    public HttpFileTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"HttpFileTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string CreateTempFile(string fileName, string content)
    {
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

    #region HttpFile.ParseFile Tests

    [Fact]
    public void ParseFile_ExistingFile_ParsesCorrectly()
    {
        var content = """
            GET https://api.example.com/users
            Authorization: Bearer token
            """;
        var filePath = CreateTempFile("test.http", content);

        var doc = HttpFile.ParseFile(filePath);

        Assert.Single(doc.Requests);
        Assert.Equal("GET", doc.Requests.First().Method);
        Assert.Equal("https://api.example.com/users", doc.Requests.First().RawUrl);
        Assert.Equal(filePath, doc.FilePath);
    }

    [Fact]
    public void ParseFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent.http");

        Assert.Throws<FileNotFoundException>(() => HttpFile.ParseFile(nonExistentPath));
    }

    [Fact]
    public void ParseFile_EmptyFile_ReturnsEmptyDocument()
    {
        var filePath = CreateTempFile("empty.http", "");

        var doc = HttpFile.ParseFile(filePath);

        Assert.Empty(doc.Items);
        Assert.Empty(doc.Requests);
    }

    [Fact]
    public void ParseFile_FileWithUtf8Bom_ParsesCorrectly()
    {
        var content = """
            GET https://api.example.com/users
            """;
        var filePath = CreateTempFile("utf8bom.http", content);
        // Write with BOM
        File.WriteAllText(filePath, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var doc = HttpFile.ParseFile(filePath);

        Assert.Single(doc.Requests);
        Assert.Equal("GET", doc.Requests.First().Method);
    }

    [Fact]
    public void ParseFile_FileWithUnicodeContent_ParsesCorrectly()
    {
        var content = """
            POST https://api.example.com/messages
            Content-Type: application/json

            {"message": "你好世界 🌍"}
            """;
        var filePath = CreateTempFile("unicode.http", content);

        var doc = HttpFile.ParseFile(filePath);

        Assert.Single(doc.Requests);
        var body = doc.Requests.First().Body as TextBody;
        Assert.NotNull(body);
        Assert.Contains("你好世界", body.Content);
        Assert.Contains("🌍", body.Content);
    }

    #endregion

    #region HttpFile.Parse(Stream) Tests

    [Fact]
    public void Parse_StreamWithFilePath_ParsesCorrectly()
    {
        var content = """
            GET https://api.example.com/users
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var doc = HttpFile.Parse(stream, "/path/to/test.http");

        Assert.Single(doc.Requests);
        Assert.Equal("GET", doc.Requests.First().Method);
        Assert.Equal("/path/to/test.http", doc.FilePath);
    }

    [Fact]
    public void Parse_StreamWithoutFilePath_ParsesCorrectly()
    {
        var content = """
            POST https://api.example.com/data
            Content-Type: application/json

            {"key": "value"}
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var doc = HttpFile.Parse(stream);

        Assert.Single(doc.Requests);
        Assert.Equal("POST", doc.Requests.First().Method);
        Assert.Null(doc.FilePath);
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsEmptyDocument()
    {
        using var stream = new MemoryStream();

        var doc = HttpFile.Parse(stream);

        Assert.Empty(doc.Items);
    }

    [Fact]
    public void Parse_StreamWithMultipleRequests_ParsesAll()
    {
        var content = """
            GET https://api.example.com/users

            ###

            POST https://api.example.com/users
            Content-Type: application/json

            {"name": "Test"}

            ###

            DELETE https://api.example.com/users/1
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var doc = HttpFile.Parse(stream, "api.http");

        Assert.Equal(3, doc.Requests.Count());
        Assert.Equal("GET", doc.Requests.ElementAt(0).Method);
        Assert.Equal("POST", doc.Requests.ElementAt(1).Method);
        Assert.Equal("DELETE", doc.Requests.ElementAt(2).Method);
    }

    #endregion

    #region HttpEnvironment.Load Tests

    [Fact]
    public void HttpEnvironment_Load_LoadsEnvironmentFile()
    {
        var envContent = """
            {
              "development": {
                "baseUrl": "http://localhost:3000"
              },
              "production": {
                "baseUrl": "https://api.example.com"
              }
            }
            """;
        CreateTempFile("http-client.env.json", envContent);

        var selector = HttpEnvironment.Load(_tempDirectory);

        Assert.Contains("development", selector.AvailableEnvironments);
        Assert.Contains("production", selector.AvailableEnvironments);
    }

    [Fact]
    public void HttpEnvironment_Load_DirectoryWithoutEnvFile_ReturnsEmptySelector()
    {
        var emptyDir = Path.Combine(_tempDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        var selector = HttpEnvironment.Load(emptyDir);

        Assert.Empty(selector.AvailableEnvironments);
    }

    [Fact]
    public void HttpEnvironment_Load_WithSharedVariables_MergesCorrectly()
    {
        var envContent = """
            {
              "$shared": {
                "apiVersion": "v1"
              },
              "development": {
                "host": "localhost"
              }
            }
            """;
        CreateTempFile("http-client.env.json", envContent);

        var selector = HttpEnvironment.Load(_tempDirectory);
        selector.SelectEnvironment("development");

        var context = selector.CreateContext();
        Assert.True(context.CanResolve("apiVersion"));
        Assert.Equal("v1", context.Resolve("apiVersion"));
        Assert.True(context.CanResolve("host"));
        Assert.Equal("localhost", context.Resolve("host"));
    }

    [Fact]
    public void HttpEnvironment_Parse_ParsesEnvironmentContent()
    {
        var envContent = """
            {
              "test": {
                "key": "value"
              }
            }
            """;

        var envFile = HttpEnvironment.Parse(envContent, "test.env.json");

        Assert.Contains("test", envFile.Environments.Keys);
        Assert.Equal("value", envFile.Environments["test"]["key"]);
    }

    [Fact]
    public void HttpEnvironment_ParseVsCodeSettings_ParsesRestClientEnvironments()
    {
        var settingsContent = """
            {
              "rest-client.environmentVariables": {
                "local": {
                  "host": "localhost"
                },
                "remote": {
                  "host": "api.example.com"
                }
              }
            }
            """;

        var envFile = HttpEnvironment.ParseVsCodeSettings(settingsContent);

        Assert.Contains("local", envFile.Environments.Keys);
        Assert.Contains("remote", envFile.Environments.Keys);
    }

    #endregion

    #region HttpRequestExtensions.Resolve Tests

    [Fact]
    public void HttpRequest_Resolve_WithoutContext_UsesDefault()
    {
        var content = """
            GET https://api.example.com/{{$guid}}/info
            """;
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var resolved = request.Resolve();

        Assert.NotNull(resolved);
        Assert.DoesNotContain("{{$guid}}", resolved.ResolvedUrl);
    }

    [Fact]
    public void HttpRequest_Resolve_WithContext_ResolvesVariables()
    {
        var content = """
            GET https://{{host}}/users
            """;
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var context = new VariableContext();
        context.AddResolver(new InMemoryVariableResolver(new Dictionary<string, string>
        {
            { "host", "api.example.com" }
        }));

        var resolved = request.Resolve(context);

        Assert.NotNull(resolved);
        Assert.Equal("https://api.example.com/users", resolved.ResolvedUrl);
    }

    [Fact]
    public void HttpRequest_Resolve_WithUnresolvedVariable_PreservesTemplate()
    {
        var content = """
            GET https://{{host}}/users
            """;
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var resolved = request.Resolve();

        Assert.Contains("{{host}}", resolved.ResolvedUrl);
        Assert.True(resolved.HasUnresolvedVariables);
    }

    #endregion

    #region HttpRequestExtensions.ToHttpRequestMessage Tests

    [Fact]
    public void HttpRequest_ToHttpRequestMessage_WithoutContext_Works()
    {
        var content = """
            GET https://api.example.com/users
            Accept: application/json
            """;
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var httpRequest = request.ToHttpRequestMessage();

        Assert.Equal(HttpMethod.Get, httpRequest.Method);
        Assert.Equal("https://api.example.com/users", httpRequest.RequestUri?.ToString());
        Assert.True(httpRequest.Headers.Contains("Accept"));
    }

    [Fact]
    public void HttpRequest_ToHttpRequestMessage_WithContext_ResolvesVariables()
    {
        var content = """
            GET https://{{host}}/users
            Authorization: Bearer {{token}}
            """;
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var context = new VariableContext();
        context.AddResolver(new InMemoryVariableResolver(new Dictionary<string, string>
        {
            { "host", "api.example.com" },
            { "token", "secret123" }
        }));

        var httpRequest = request.ToHttpRequestMessage(context);

        Assert.Equal("https://api.example.com/users", httpRequest.RequestUri?.ToString());
        Assert.Equal("Bearer secret123", httpRequest.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task HttpRequest_ToHttpRequestMessage_WithBody_IncludesContent()
    {
        var content = """
            POST https://api.example.com/users
            Content-Type: application/json

            {"name": "John"}
            """;
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var httpRequest = request.ToHttpRequestMessage();

        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("John", body);
    }

    [Fact]
    public void HttpRequest_ToHttpRequestMessage_AllHttpMethods_Work()
    {
        var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };

        foreach (var method in methods)
        {
            var content = $"{method} https://api.example.com/test";
            var doc = HttpFile.Parse(content);
            var httpRequest = doc.Requests.First().ToHttpRequestMessage();

            Assert.Equal(method, httpRequest.Method.Method);
        }
    }

    #endregion

    #region HttpDocumentExtensions Tests

    [Fact]
    public void HttpDocument_CreateVariableContext_IncludesFileVariables()
    {
        var content = """
            @host = api.example.com
            @token = secret

            GET https://{{host}}/users
            """;
        var doc = HttpFile.Parse(content);

        var context = doc.CreateVariableContext();

        Assert.True(context.CanResolve("host"));
        Assert.Equal("api.example.com", context.Resolve("host"));
        Assert.True(context.CanResolve("token"));
        Assert.Equal("secret", context.Resolve("token"));
    }

    [Fact]
    public void HttpDocument_ResolveVariables_WithoutContext_UsesDocumentContext()
    {
        var content = """
            @baseUrl = https://api.example.com

            GET {{baseUrl}}/users
            """;
        var doc = HttpFile.Parse(content);

        var resolved = doc.ResolveVariables();

        Assert.Single(resolved.Requests);
        Assert.Equal("https://api.example.com/users", resolved.Requests.First().ResolvedUrl);
    }

    [Fact]
    public void HttpDocument_ResolveVariables_WithContext_OverridesFileVariables()
    {
        var content = """
            @host = localhost

            GET https://{{host}}/users
            """;
        var doc = HttpFile.Parse(content);

        var context = new VariableContext();
        context.AddResolver(new InMemoryVariableResolver(new Dictionary<string, string>
        {
            { "host", "api.example.com" }
        }));

        var resolved = doc.ResolveVariables(context);

        Assert.Equal("https://api.example.com/users", resolved.Requests.First().ResolvedUrl);
    }

    #endregion

    #region Helper class for tests

    private class InMemoryVariableResolver : IVariableResolver
    {
        private readonly Dictionary<string, string> _variables;

        public InMemoryVariableResolver(Dictionary<string, string> variables)
        {
            _variables = variables;
        }

        public string? Resolve(string name)
        {
            return _variables.TryGetValue(name, out var value) ? value : null;
        }

        public bool CanResolve(string name)
        {
            return _variables.ContainsKey(name);
        }
    }

    #endregion
}
