using System.Text.Json;
using HttpFileParser.Variables;

namespace HttpFileParser.Tests;

public class RequestVariableResolverTests
{
    [Fact]
    public void Resolve_BodyJson_ExtractsValue()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"id": 123, "name": "John"}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body.$.id");

        Assert.Equal("123", result);
    }

    [Fact]
    public void Resolve_BodyJsonNested_ExtractsValue()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"user": {"profile": {"name": "John"}}}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body.$.user.profile.name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_Headers_ExtractsValue()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "login",
            200,
            new Dictionary<string, string> { ["Authorization"] = "Bearer token123" },
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("login.response.headers.Authorization");

        Assert.Equal("Bearer token123", result);
    }

    [Fact]
    public void CanResolve_ExistingRequest_ReturnsTrue()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse("test", 200, new Dictionary<string, string>(), "{}", null));

        var resolver = new RequestVariableResolver(provider);

        Assert.True(resolver.CanResolve("test.response.body"));
    }

    [Fact]
    public void CanResolve_NonExistingRequest_ReturnsFalse()
    {
        var provider = new InMemoryResponseProvider();
        var resolver = new RequestVariableResolver(provider);

        Assert.False(resolver.CanResolve("nonexistent.response.body"));
    }

    [Fact]
    public void Resolve_FullBody_ReturnsEntireBody()
    {
        var provider = new InMemoryResponseProvider();
        var body = """{"status": "ok"}""";
        provider.AddResponse(new RequestResponse("test", 200, new Dictionary<string, string>(), body, "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body");

        Assert.Equal(body, result);
    }

    [Fact]
    public void InMemoryResponseProvider_ManagesResponses()
    {
        var provider = new InMemoryResponseProvider();

        provider.AddResponse(new RequestResponse("test", 200, new Dictionary<string, string>(), "{}", null));
        Assert.True(provider.HasResponse("test"));

        provider.RemoveResponse("test");
        Assert.False(provider.HasResponse("test"));

        provider.AddResponse(new RequestResponse("test1", 200, new Dictionary<string, string>(), "{}", null));
        provider.AddResponse(new RequestResponse("test2", 200, new Dictionary<string, string>(), "{}", null));
        provider.Clear();
        Assert.False(provider.HasResponse("test1"));
        Assert.False(provider.HasResponse("test2"));
    }
}
