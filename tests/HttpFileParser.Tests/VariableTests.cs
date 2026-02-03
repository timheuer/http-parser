using HttpFileParser.Variables;

namespace HttpFileParser.Tests;

public class VariableTests
{
    [Fact]
    public void FileVariableResolver_ResolvesDefinedVariable()
    {
        var resolver = new FileVariableResolver();
        resolver.SetVariable("baseUrl", "https://api.example.com");

        Assert.Equal("https://api.example.com", resolver.Resolve("baseUrl"));
        Assert.True(resolver.CanResolve("baseUrl"));
    }

    [Fact]
    public void FileVariableResolver_ReturnsNullForUndefined()
    {
        var resolver = new FileVariableResolver();

        Assert.Null(resolver.Resolve("undefined"));
        Assert.False(resolver.CanResolve("undefined"));
    }

    [Fact]
    public void EnvironmentVariableResolver_ResolvesVariables()
    {
        var resolver = new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["apiKey"] = "secret123"
        });

        Assert.Equal("secret123", resolver.Resolve("apiKey"));
    }

    [Fact]
    public void DynamicVariableResolver_ResolvesGuid()
    {
        var resolver = new DynamicVariableResolver();

        var result = resolver.Resolve("$guid");

        Assert.NotNull(result);
        Assert.True(Guid.TryParse(result, out _));
    }

    [Fact]
    public void DynamicVariableResolver_ResolvesTimestamp()
    {
        var resolver = new DynamicVariableResolver();

        var result = resolver.Resolve("$timestamp");

        Assert.NotNull(result);
        Assert.True(long.TryParse(result, out var timestamp));
        Assert.True(timestamp > 0);
    }

    [Fact]
    public void DynamicVariableResolver_ResolvesRandomInt()
    {
        var resolver = new DynamicVariableResolver();

        var result = resolver.Resolve("$randomInt 1 10");

        Assert.NotNull(result);
        Assert.True(int.TryParse(result, out var value));
        Assert.InRange(value, 1, 10);
    }

    [Fact]
    public void DynamicVariableResolver_ResolvesProcessEnv()
    {
        System.Environment.SetEnvironmentVariable("TEST_VAR", "test_value");
        var resolver = new DynamicVariableResolver();

        var result = resolver.Resolve("$processEnv TEST_VAR");

        Assert.Equal("test_value", result);
    }

    [Fact]
    public void VariableContext_UsesPrecedence()
    {
        var context = new VariableContext();
        context.AddResolver(new FileVariableResolver(new Dictionary<string, string> { ["var"] = "file" }.Select(kvp => new HttpFileParser.Model.FileVariable(kvp.Key, kvp.Value, default))));
        context.AddResolver(new EnvironmentVariableResolver(new Dictionary<string, string> { ["var"] = "env" }));

        // File resolver added first, so it takes precedence
        Assert.Equal("file", context.Resolve("var"));
    }

    [Fact]
    public void VariableExpander_ExpandsSimpleVariable()
    {
        var context = new VariableContext();
        context.AddResolver(new FileVariableResolver(new Dictionary<string, string> { ["name"] = "John" }.Select(kvp => new HttpFileParser.Model.FileVariable(kvp.Key, kvp.Value, default))));

        var expander = new VariableExpander(context);
        var result = expander.Expand("Hello, {{name}}!");

        Assert.Equal("Hello, John!", result);
    }

    [Fact]
    public void VariableExpander_ExpandsMultipleVariables()
    {
        var context = new VariableContext();
        context.AddResolver(new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["host"] = "api.example.com",
            ["version"] = "v1"
        }));

        var expander = new VariableExpander(context);
        var result = expander.Expand("https://{{host}}/{{version}}/users");

        Assert.Equal("https://api.example.com/v1/users", result);
    }

    [Fact]
    public void VariableExpander_PreservesUnresolvedVariables()
    {
        var context = new VariableContext();
        var expander = new VariableExpander(context);

        var result = expander.Expand("Hello, {{unknown}}!", out var unresolved);

        Assert.Equal("Hello, {{unknown}}!", result);
        Assert.Contains("unknown", unresolved);
    }

    [Fact]
    public void VariableExpander_HandlesPercentEncoding()
    {
        var context = new VariableContext();
        context.AddResolver(new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["query"] = "hello world"
        }));

        var expander = new VariableExpander(context);
        var result = expander.Expand("?q={{%query}}");

        Assert.Equal("?q=hello+world", result);
    }

    [Fact]
    public void VariableExpander_HandlesRecursiveVariables()
    {
        var context = new VariableContext();
        context.AddResolver(new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["baseUrl"] = "https://{{host}}/api",
            ["host"] = "example.com"
        }));

        var expander = new VariableExpander(context);
        var result = expander.Expand("{{baseUrl}}/users");

        Assert.Equal("https://example.com/api/users", result);
    }

    [Fact]
    public void VariableExpander_ContainsVariables_DetectsVariables()
    {
        Assert.True(VariableExpander.ContainsVariables("{{variable}}"));
        Assert.False(VariableExpander.ContainsVariables("no variables"));
    }

    [Fact]
    public void VariableExpander_ExtractVariableNames_ExtractsCorrectly()
    {
        var names = VariableExpander.ExtractVariableNames("{{var1}} and {{var2}}").ToList();

        Assert.Contains("var1", names);
        Assert.Contains("var2", names);
    }
}
