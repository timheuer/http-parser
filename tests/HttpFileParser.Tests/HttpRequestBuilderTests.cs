using HttpFileParser.Execution;
using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Tests;

public class HttpRequestBuilderTests
{
    [Fact]
    public void Build_SimpleRequest_CreatesHttpRequestMessage()
    {
        var content = "GET https://api.example.com/users";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Equal(HttpMethod.Get, httpRequest.Method);
        Assert.Equal("https://api.example.com/users", httpRequest.RequestUri?.ToString());
    }

    [Fact]
    public void Build_RequestWithHeaders_SetsHeaders()
    {
        var content = """
            GET https://api.example.com/users
            Authorization: Bearer token123
            Accept: application/json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.True(httpRequest.Headers.Contains("Authorization"));
        Assert.Equal("Bearer token123", httpRequest.Headers.GetValues("Authorization").First());
    }

    [Fact]
    public async Task Build_PostWithBody_SetsContent()
    {
        var content = """
            POST https://api.example.com/users
            Content-Type: application/json

            {"name": "John"}
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("John", body);
    }

    [Fact]
    public void Build_WithVariables_ResolvesVariables()
    {
        var content = """
            @baseUrl = https://api.example.com

            GET {{baseUrl}}/users
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var context = doc.CreateVariableContext();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request, context);

        Assert.Equal("https://api.example.com/users", httpRequest.RequestUri?.ToString());
    }

    [Fact]
    public void Build_AllMethods_CreatesCorrectMethod()
    {
        var methods = new[]
        {
            ("GET", HttpMethod.Get),
            ("POST", HttpMethod.Post),
            ("PUT", HttpMethod.Put),
            ("DELETE", HttpMethod.Delete),
            ("PATCH", HttpMethod.Patch),
            ("HEAD", HttpMethod.Head),
            ("OPTIONS", HttpMethod.Options)
        };

        foreach (var (methodStr, expectedMethod) in methods)
        {
            var content = $"{methodStr} https://api.example.com/test";
            var doc = HttpFile.Parse(content);
            var request = doc.Requests.First();

            var builder = new HttpRequestBuilder();
            var httpRequest = builder.Build(request);

            Assert.Equal(expectedMethod, httpRequest.Method);
        }
    }

    [Fact]
    public void ResolveVariables_TracksUnresolvedVariables()
    {
        var content = """
            GET https://api.example.com/users/{{userId}}
            Authorization: Bearer {{token}}
            """;

        var doc = HttpFile.Parse(content);
        var resolved = doc.ResolveVariables();

        Assert.True(resolved.HasUnresolvedVariables);
        Assert.Contains("userId", resolved.AllUnresolvedVariables);
        Assert.Contains("token", resolved.AllUnresolvedVariables);
    }

    [Fact]
    public void ResolvedHttpRequest_ToHttpRequestMessage_Works()
    {
        var content = """
            @baseUrl = https://api.example.com

            GET {{baseUrl}}/users
            Accept: application/json
            """;

        var doc = HttpFile.Parse(content);
        var resolved = doc.ResolveVariables();
        var httpRequest = resolved.Requests.First().ToHttpRequestMessage();

        Assert.Equal("https://api.example.com/users", httpRequest.RequestUri?.ToString());
    }

    [Fact]
    public void Build_DetectsJsonContentType()
    {
        var content = """
            POST https://api.example.com/users

            {"name": "John"}
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Contains("json", httpRequest.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public void Build_DetectsXmlContentType()
    {
        var content = """
            POST https://api.example.com/users

            <?xml version="1.0"?>
            <user><name>John</name></user>
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Contains("xml", httpRequest.Content.Headers.ContentType?.MediaType ?? "");
    }
}
