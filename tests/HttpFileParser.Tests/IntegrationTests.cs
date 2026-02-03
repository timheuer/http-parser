namespace HttpFileParser.Tests;

public class IntegrationTests
{
    [Fact]
    public void FullWorkflow_ParseResolveAndBuild()
    {
        var httpContent = """
            @baseUrl = https://api.example.com
            @token = secret-token-123

            ### Get Users
            # @name getUsers
            GET {{baseUrl}}/users
            Authorization: Bearer {{token}}
            Accept: application/json
            """;

        // Parse
        var doc = HttpFile.Parse(httpContent);
        Assert.Single(doc.Requests);
        Assert.Equal(2, doc.Variables.Count());

        // Resolve
        var context = doc.CreateVariableContext();
        var resolved = doc.ResolveVariables(context);
        Assert.False(resolved.HasUnresolvedVariables);

        // Build
        var httpRequest = resolved.Requests.First().ToHttpRequestMessage();
        Assert.Equal("https://api.example.com/users", httpRequest.RequestUri?.ToString());
        Assert.Equal("Bearer secret-token-123", httpRequest.Headers.Authorization?.ToString());
    }

    [Fact]
    public void FullWorkflow_WithEnvironment()
    {
        var envJson = """
            {
              "$shared": {
                "apiVersion": "v1"
              },
              "development": {
                "baseUrl": "http://localhost:3000"
              },
              "production": {
                "baseUrl": "https://api.example.com"
              }
            }
            """;

        var httpContent = """
            GET {{baseUrl}}/{{apiVersion}}/users
            """;

        // Parse environment
        var envFile = HttpEnvironment.Parse(envJson);
        var selector = new HttpFileParser.Environment.EnvironmentSelector();
        selector.AddEnvironmentFile(envFile);
        selector.SelectEnvironment("production");

        // Parse HTTP file
        var doc = HttpFile.Parse(httpContent);

        // Create combined context
        var context = selector.CreateContext();

        // Resolve and build
        var resolved = doc.ResolveVariables(context);
        var httpRequest = resolved.Requests.First().ToHttpRequestMessage();

        Assert.Equal("https://api.example.com/v1/users", httpRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task FullWorkflow_DynamicVariables()
    {
        var httpContent = """
            POST https://api.example.com/events
            Content-Type: application/json
            X-Request-Id: {{$guid}}

            {"timestamp": {{$timestamp}}}
            """;

        var doc = HttpFile.Parse(httpContent);
        var resolved = doc.ResolveVariables();

        // Dynamic variables should be resolved
        Assert.False(resolved.HasUnresolvedVariables);

        var httpRequest = resolved.Requests.First().ToHttpRequestMessage();

        // X-Request-Id should have a GUID value
        Assert.True(httpRequest.Headers.Contains("X-Request-Id"));
        var requestId = httpRequest.Headers.GetValues("X-Request-Id").First();
        Assert.True(Guid.TryParse(requestId, out _));

        // Body should have timestamp
        var body = httpRequest.Content != null ? await httpRequest.Content.ReadAsStringAsync() : null;
        Assert.NotNull(body);
        Assert.Contains("timestamp", body);
    }

    [Fact]
    public void FullWorkflow_MultipleRequestsWithVariables()
    {
        var httpContent = """
            @baseUrl = https://api.example.com

            ### Get Users
            # @name getUsers
            GET {{baseUrl}}/users

            ###

            ### Create User
            # @name createUser
            POST {{baseUrl}}/users
            Content-Type: application/json

            {"name": "New User"}
            """;

        var doc = HttpFile.Parse(httpContent);
        Assert.Equal(2, doc.Requests.Count());

        var resolved = doc.ResolveVariables();

        // Get user request
        var getRequest = resolved.GetRequestByName("getUsers");
        Assert.NotNull(getRequest);
        Assert.Equal("https://api.example.com/users", getRequest.ResolvedUrl);

        // Create user request
        var createRequest = resolved.GetRequestByName("createUser");
        Assert.NotNull(createRequest);
        Assert.Equal("https://api.example.com/users", createRequest.ResolvedUrl);
    }

    [Fact]
    public void EdgeCase_EmptyBody()
    {
        var httpContent = """
            DELETE https://api.example.com/users/123
            """;

        var doc = HttpFile.Parse(httpContent);
        var request = doc.Requests.First();

        Assert.Null(request.Body);

        var httpRequest = request.ToHttpRequestMessage();
        Assert.Null(httpRequest.Content);
    }

    [Fact]
    public void EdgeCase_HeadersOnly()
    {
        var httpContent = """
            GET https://api.example.com/users
            Authorization: Bearer token
            X-Custom-Header: value1
            X-Another-Header: value2
            Accept: application/json
            Accept-Language: en-US
            """;

        var doc = HttpFile.Parse(httpContent);
        var request = doc.Requests.First();

        Assert.Equal(5, request.Headers.Count);
    }

    [Fact]
    public async Task EdgeCase_UnicodeContent()
    {
        var httpContent = """
            POST https://api.example.com/messages
            Content-Type: application/json

            {"message": "Hello, 世界! 🌍"}
            """;

        var doc = HttpFile.Parse(httpContent);
        var httpRequest = doc.Requests.First().ToHttpRequestMessage();

        var body = httpRequest.Content != null ? await httpRequest.Content.ReadAsStringAsync() : null;
        Assert.NotNull(body);
        Assert.Contains("世界", body);
        Assert.Contains("🌍", body);
    }
}
