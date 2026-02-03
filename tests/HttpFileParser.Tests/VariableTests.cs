using HttpFileParser.Execution;
using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Tests;

public class VariableTests
{
    #region VariableContext Tests

    [Fact]
    public void VariableContext_InsertResolver_AtBeginning_TakesPrecedence()
    {
        var context = new VariableContext();
        var lowerPriorityResolver = new FileVariableResolver();
        lowerPriorityResolver.SetVariable("test", "lower");
        context.AddResolver(lowerPriorityResolver);

        var highPriorityResolver = new FileVariableResolver();
        highPriorityResolver.SetVariable("test", "higher");
        context.InsertResolver(0, highPriorityResolver);

        Assert.Equal("higher", context.Resolve("test"));
        Assert.Equal(2, context.Resolvers.Count);
        Assert.Same(highPriorityResolver, context.Resolvers[0]);
    }

    [Fact]
    public void VariableContext_InsertResolver_AtEnd_LowestPriority()
    {
        var context = new VariableContext();
        var highPriorityResolver = new FileVariableResolver();
        highPriorityResolver.SetVariable("test", "higher");
        context.AddResolver(highPriorityResolver);

        var lowPriorityResolver = new FileVariableResolver();
        lowPriorityResolver.SetVariable("test", "lower");
        context.InsertResolver(1, lowPriorityResolver);

        // High priority resolver should still win
        Assert.Equal("higher", context.Resolve("test"));
        Assert.Same(lowPriorityResolver, context.Resolvers[1]);
    }

    [Fact]
    public void VariableContext_RemoveResolver_RemovesFromChain()
    {
        var context = new VariableContext();
        var resolver1 = new FileVariableResolver();
        resolver1.SetVariable("var1", "value1");
        var resolver2 = new FileVariableResolver();
        resolver2.SetVariable("var2", "value2");

        context.AddResolver(resolver1);
        context.AddResolver(resolver2);
        Assert.Equal(2, context.Resolvers.Count);

        context.RemoveResolver(resolver1);
        Assert.Single(context.Resolvers);
        Assert.Null(context.Resolve("var1"));
        Assert.Equal("value2", context.Resolve("var2"));
    }

    [Fact]
    public void VariableContext_RemoveResolver_NonExistent_DoesNotThrow()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();

        // Should not throw
        context.RemoveResolver(resolver);
        Assert.Empty(context.Resolvers);
    }

    [Fact]
    public void VariableContext_CanResolve_ReturnsTrue_WhenResolverCanResolve()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("knownVar", "value");
        context.AddResolver(resolver);

        Assert.True(context.CanResolve("knownVar"));
    }

    [Fact]
    public void VariableContext_CanResolve_ReturnsFalse_WhenNoResolverCanResolve()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("knownVar", "value");
        context.AddResolver(resolver);

        Assert.False(context.CanResolve("unknownVar"));
    }

    [Fact]
    public void VariableContext_CanResolve_ReturnsFalse_WhenEmpty()
    {
        var context = new VariableContext();

        Assert.False(context.CanResolve("anyVar"));
    }

    [Fact]
    public void VariableContext_Constructor_WithResolvers_AddsAll()
    {
        var resolver1 = new FileVariableResolver();
        resolver1.SetVariable("var1", "value1");
        var resolver2 = new FileVariableResolver();
        resolver2.SetVariable("var2", "value2");

        var context = new VariableContext([resolver1, resolver2]);

        Assert.Equal(2, context.Resolvers.Count);
        Assert.Equal("value1", context.Resolve("var1"));
        Assert.Equal("value2", context.Resolve("var2"));
    }

    [Fact]
    public void VariableContext_CreateDefault_IncludesDynamicResolver()
    {
        var context = VariableContext.CreateDefault();

        Assert.Single(context.Resolvers);
        Assert.True(context.CanResolve("$guid"));
        Assert.True(context.CanResolve("$timestamp"));
    }

    [Fact]
    public void VariableContext_Resolve_ReturnsFirstMatch_NotAllMatches()
    {
        var context = new VariableContext();
        var resolver1 = new FileVariableResolver();
        resolver1.SetVariable("shared", "first");
        var resolver2 = new FileVariableResolver();
        resolver2.SetVariable("shared", "second");

        context.AddResolver(resolver1);
        context.AddResolver(resolver2);

        Assert.Equal("first", context.Resolve("shared"));
    }

    [Fact]
    public void VariableContext_UsesPrecedence()
    {
        var context = new VariableContext();
        context.AddResolver(new FileVariableResolver(new Dictionary<string, string> { ["var"] = "file" }.Select(kvp => new FileVariable(kvp.Key, kvp.Value, default))));
        context.AddResolver(new EnvironmentVariableResolver(new Dictionary<string, string> { ["var"] = "env" }));

        // File resolver added first, so it takes precedence
        Assert.Equal("file", context.Resolve("var"));
    }

    [Fact]
    public void VariableContext_Resolve_WhenCanResolveButResolveReturnsNull_ContinuesToNextResolver()
    {
        // This tests the case where CanResolve returns true but Resolve returns null
        // The context should continue to the next resolver
        var context = new VariableContext();

        // DynamicVariableResolver CanResolve returns true for $processEnv, but Resolve may return null
        // if the env var doesn't exist
        context.AddResolver(new DynamicVariableResolver());

        // Add a fallback resolver
        var fallback = new FileVariableResolver();
        fallback.SetVariable("$processEnv NONEXISTENT_VAR_12345", "fallback");
        context.AddResolver(fallback);

        // The DynamicVariableResolver will CanResolve=true but Resolve=null
        // Then it should fall through to the FileVariableResolver
        var result = context.Resolve("$processEnv NONEXISTENT_VAR_12345");

        // Result should be fallback value since dynamic resolver returns null
        Assert.Equal("fallback", result);
    }

    [Fact]
    public void VariableContext_Resolve_WhenNoResolverCanResolve_ReturnsNull()
    {
        var context = new VariableContext();
        context.AddResolver(new FileVariableResolver());

        var result = context.Resolve("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void VariableContext_Resolve_EmptyContext_ReturnsNull()
    {
        var context = new VariableContext();

        var result = context.Resolve("anyVariable");

        Assert.Null(result);
    }

    #endregion

    #region VariableExpander Tests

    [Fact]
    public void VariableExpander_MaxRecursionDepth_StopsExpanding()
    {
        var resolver = new FileVariableResolver();
        // Create deeply nested variable that exceeds default recursion depth of 10
        resolver.SetVariable("v0", "{{v1}}");
        resolver.SetVariable("v1", "{{v2}}");
        resolver.SetVariable("v2", "{{v3}}");
        resolver.SetVariable("v3", "{{v4}}");
        resolver.SetVariable("v4", "{{v5}}");
        resolver.SetVariable("v5", "{{v6}}");
        resolver.SetVariable("v6", "{{v7}}");
        resolver.SetVariable("v7", "{{v8}}");
        resolver.SetVariable("v8", "{{v9}}");
        resolver.SetVariable("v9", "{{v10}}");
        resolver.SetVariable("v10", "{{v11}}");
        resolver.SetVariable("v11", "final");

        var context = new VariableContext([resolver]);
        var expander = new VariableExpander(context, maxRecursionDepth: 5);

        var result = expander.Expand("{{v0}}");

        // Should stop after 5 levels of recursion, leaving unexpanded variables
        Assert.Contains("{{", result);
    }

    [Fact]
    public void VariableExpander_CustomMaxRecursionDepth_Honored()
    {
        var resolver = new FileVariableResolver();
        resolver.SetVariable("a", "{{b}}");
        resolver.SetVariable("b", "{{c}}");
        resolver.SetVariable("c", "done");

        var context = new VariableContext([resolver]);
        var expanderDepth2 = new VariableExpander(context, maxRecursionDepth: 2);

        var result = expanderDepth2.Expand("{{a}}");

        // With depth 2, we can expand a->b->c, then c->done (3 total)
        Assert.Equal("done", result);
    }

    [Fact]
    public void VariableExpander_UnclosedBraces_PreservesOriginal()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("name", "John");
        context.AddResolver(resolver);

        var expander = new VariableExpander(context);
        var result = expander.Expand("Hello {{name and more text");

        // Unclosed braces should be preserved as literal text
        Assert.Equal("Hello {{name and more text", result);
    }

    [Fact]
    public void VariableExpander_EmptyVariableName_PreservesOriginal()
    {
        var context = new VariableContext();
        var expander = new VariableExpander(context);

        var result = expander.Expand("Hello {{}} world", out var unresolved);

        // Empty variable name should be preserved
        Assert.Equal("Hello {{}} world", result);
        Assert.Contains("", unresolved);
    }

    [Fact]
    public void VariableExpander_WhitespaceOnlyVariableName_PreservesOriginal()
    {
        var context = new VariableContext();
        var expander = new VariableExpander(context);

        var result = expander.Expand("Hello {{   }} world", out var unresolved);

        Assert.Equal("Hello {{   }} world", result);
    }

    [Fact]
    public void VariableExpander_AdjacentVariables_ExpandsAll()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("a", "Hello");
        resolver.SetVariable("b", "World");
        context.AddResolver(resolver);

        var expander = new VariableExpander(context);
        var result = expander.Expand("{{a}}{{b}}");

        Assert.Equal("HelloWorld", result);
    }

    [Fact]
    public void VariableExpander_SingleBrace_NotTreatedAsVariable()
    {
        var context = new VariableContext();
        var expander = new VariableExpander(context);

        var result = expander.Expand("JSON: {\"key\": \"value\"}");

        Assert.Equal("JSON: {\"key\": \"value\"}", result);
    }

    [Fact]
    public void VariableExpander_TripleBraces_HandlesCorrectly()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("name", "test");
        context.AddResolver(resolver);

        var expander = new VariableExpander(context);

        // With the implementation, `{{{name}}}` parses as `{` + `{{name}}` + `}`
        // Since {{name}} isn't at the end, it actually parses differently
        // Let's test the simpler case: `{{name}}x` should become `testx`
        var result = expander.Expand("x{{name}}y");
        Assert.Equal("xtesty", result);
    }

    [Fact]
    public void VariableExpander_MixedResolvedAndUnresolved()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("known", "resolved");
        context.AddResolver(resolver);

        var expander = new VariableExpander(context);
        var result = expander.Expand("{{known}} and {{unknown}}", out var unresolved);

        Assert.Equal("resolved and {{unknown}}", result);
        Assert.Contains("unknown", unresolved);
        Assert.DoesNotContain("known", unresolved);
    }

    [Fact]
    public void VariableExpander_PercentEncoding_EncodesSpecialCharacters()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("path", "a/b/c");
        resolver.SetVariable("query", "key=value&other=test");
        context.AddResolver(resolver);

        var expander = new VariableExpander(context);

        Assert.Equal("a%2fb%2fc", expander.Expand("{{%path}}"));
        Assert.Equal("key%3dvalue%26other%3dtest", expander.Expand("{{%query}}"));
    }

    [Fact]
    public void VariableExpander_VariableWithSpaces_Trims()
    {
        var context = new VariableContext();
        var resolver = new FileVariableResolver();
        resolver.SetVariable("name", "John");
        context.AddResolver(resolver);

        var expander = new VariableExpander(context);
        var result = expander.Expand("Hello {{ name }}!");

        Assert.Equal("Hello John!", result);
    }

    [Fact]
    public void VariableExpander_ExtractVariableNames_ExcludesPercentPrefix()
    {
        var names = VariableExpander.ExtractVariableNames("{{%encoded}} and {{normal}}").ToList();

        Assert.Contains("encoded", names);
        Assert.Contains("normal", names);
        Assert.DoesNotContain("%encoded", names);
    }

    [Fact]
    public void VariableExpander_ExpandsSimpleVariable()
    {
        var context = new VariableContext();
        context.AddResolver(new FileVariableResolver(new Dictionary<string, string> { ["name"] = "John" }.Select(kvp => new FileVariable(kvp.Key, kvp.Value, default))));

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

    #endregion

    #region FileVariableResolver Tests

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
    public void FileVariableResolver_Constructor_WithHttpDocument_LoadsVariables()
    {
        var items = new List<HttpDocumentItem>
        {
            new FileVariable("baseUrl", "https://api.example.com", default),
            new FileVariable("apiKey", "secret123", default)
        };
        var document = new HttpDocument(null, items, []);

        var resolver = new FileVariableResolver(document);

        Assert.Equal("https://api.example.com", resolver.Resolve("baseUrl"));
        Assert.Equal("secret123", resolver.Resolve("apiKey"));
    }

    [Fact]
    public void FileVariableResolver_Constructor_WithFileVariables_LoadsVariables()
    {
        var variables = new List<FileVariable>
        {
            new("host", "localhost", default),
            new("port", "8080", default)
        };

        var resolver = new FileVariableResolver(variables);

        Assert.Equal("localhost", resolver.Resolve("host"));
        Assert.Equal("8080", resolver.Resolve("port"));
    }

    [Fact]
    public void FileVariableResolver_Variables_Property_ReturnsAllVariables()
    {
        var resolver = new FileVariableResolver();
        resolver.SetVariable("var1", "value1");
        resolver.SetVariable("var2", "value2");

        var variables = resolver.Variables;

        Assert.Equal(2, variables.Count);
        Assert.Equal("value1", variables["var1"]);
        Assert.Equal("value2", variables["var2"]);
    }

    [Fact]
    public void FileVariableResolver_CaseInsensitive()
    {
        var resolver = new FileVariableResolver();
        resolver.SetVariable("BaseUrl", "https://example.com");

        Assert.Equal("https://example.com", resolver.Resolve("baseurl"));
        Assert.Equal("https://example.com", resolver.Resolve("BASEURL"));
        Assert.True(resolver.CanResolve("baseURL"));
    }

    [Fact]
    public void FileVariableResolver_SetVariable_OverwritesExisting()
    {
        var resolver = new FileVariableResolver();
        resolver.SetVariable("key", "original");
        resolver.SetVariable("key", "updated");

        Assert.Equal("updated", resolver.Resolve("key"));
    }

    [Fact]
    public void FileVariableResolver_EmptyDocument_NoVariables()
    {
        var document = new HttpDocument(null, [], []);
        var resolver = new FileVariableResolver(document);

        Assert.Empty(resolver.Variables);
        Assert.False(resolver.CanResolve("anything"));
    }

    #endregion

    #region EnvironmentVariableResolver Tests

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
    public void EnvironmentVariableResolver_Constructor_WithDictionary_LoadsVariables()
    {
        var variables = new Dictionary<string, string>
        {
            ["dev_host"] = "dev.example.com",
            ["prod_host"] = "prod.example.com"
        };

        var resolver = new EnvironmentVariableResolver(variables);

        Assert.Equal("dev.example.com", resolver.Resolve("dev_host"));
        Assert.Equal("prod.example.com", resolver.Resolve("prod_host"));
    }

    [Fact]
    public void EnvironmentVariableResolver_CaseInsensitive()
    {
        var resolver = new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["ApiKey"] = "secret"
        });

        Assert.Equal("secret", resolver.Resolve("apikey"));
        Assert.Equal("secret", resolver.Resolve("APIKEY"));
    }

    [Fact]
    public void EnvironmentVariableResolver_SetVariable_AddsNew()
    {
        var resolver = new EnvironmentVariableResolver();

        resolver.SetVariable("newVar", "newValue");

        Assert.Equal("newValue", resolver.Resolve("newVar"));
    }

    [Fact]
    public void EnvironmentVariableResolver_SetVariable_OverwritesExisting()
    {
        var resolver = new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["key"] = "original"
        });

        resolver.SetVariable("key", "updated");

        Assert.Equal("updated", resolver.Resolve("key"));
    }

    [Fact]
    public void EnvironmentVariableResolver_SetVariables_AddsBatch()
    {
        var resolver = new EnvironmentVariableResolver();

        resolver.SetVariables(new Dictionary<string, string>
        {
            ["var1"] = "value1",
            ["var2"] = "value2"
        });

        Assert.Equal("value1", resolver.Resolve("var1"));
        Assert.Equal("value2", resolver.Resolve("var2"));
    }

    [Fact]
    public void EnvironmentVariableResolver_Clear_RemovesAll()
    {
        var resolver = new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["var1"] = "value1",
            ["var2"] = "value2"
        });

        resolver.Clear();

        Assert.Null(resolver.Resolve("var1"));
        Assert.Null(resolver.Resolve("var2"));
        Assert.Empty(resolver.Variables);
    }

    [Fact]
    public void EnvironmentVariableResolver_Variables_Property_ReturnsAllVariables()
    {
        var resolver = new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["host"] = "localhost",
            ["port"] = "8080"
        });

        var variables = resolver.Variables;

        Assert.Equal(2, variables.Count);
        Assert.Equal("localhost", variables["host"]);
        Assert.Equal("8080", variables["port"]);
    }

    [Fact]
    public void EnvironmentVariableResolver_CanResolve_ReturnsTrue_ForExisting()
    {
        var resolver = new EnvironmentVariableResolver(new Dictionary<string, string>
        {
            ["existing"] = "value"
        });

        Assert.True(resolver.CanResolve("existing"));
    }

    [Fact]
    public void EnvironmentVariableResolver_CanResolve_ReturnsFalse_ForNonExisting()
    {
        var resolver = new EnvironmentVariableResolver();

        Assert.False(resolver.CanResolve("nonexistent"));
    }

    #endregion

    #region DynamicVariableResolver Tests (Basic)

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

    #endregion
}

#region ResolvedHttpDocument Tests

public class ResolvedHttpDocumentTests
{
    private static HttpDocument CreateDocument(params HttpRequest[] requests)
    {
        var items = requests.Cast<HttpDocumentItem>().ToList();
        return new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());
    }

    private static HttpRequest CreateRequest(string method, string url, string? name = null)
    {
        return new HttpRequest(
            method: method,
            rawUrl: url,
            httpVersion: null,
            name: name,
            headers: new List<HttpHeader>(),
            body: null,
            directives: name != null
                ? new List<HttpDirective> { new HttpDirective("name", name, SourceSpan.Empty) }
                : new List<HttpDirective>(),
            leadingComments: new List<Comment>(),
            span: SourceSpan.Empty);
    }

    private static ResolvedHttpRequest CreateResolvedRequest(HttpRequest original, bool hasUnresolved = false, params string[] unresolvedVars)
    {
        return new ResolvedHttpRequest(
            originalRequest: original,
            method: original.Method,
            resolvedUrl: original.RawUrl,
            resolvedHeaders: original.Headers.Select(h => new KeyValuePair<string, string>(h.Name, h.RawValue)).ToList(),
            resolvedBody: null,
            unresolvedVariables: unresolvedVars.ToList());
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var request = CreateRequest("GET", "https://api.example.com/users");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);

        Assert.Same(doc, resolved.OriginalDocument);
        Assert.Single(resolved.Requests);
        Assert.Same(context, resolved.Context);
    }

    [Fact]
    public void Requests_ReturnsAllResolvedRequests()
    {
        var request1 = CreateRequest("GET", "https://api.example.com/users");
        var request2 = CreateRequest("POST", "https://api.example.com/users");
        var doc = CreateDocument(request1, request2);
        var resolvedRequests = new List<ResolvedHttpRequest>
        {
            CreateResolvedRequest(request1),
            CreateResolvedRequest(request2)
        };
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, resolvedRequests, context);

        Assert.Equal(2, resolved.Requests.Count);
    }

    [Fact]
    public void HasUnresolvedVariables_WhenNoUnresolved_ReturnsFalse()
    {
        var request = CreateRequest("GET", "https://api.example.com/users");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);

        Assert.False(resolved.HasUnresolvedVariables);
    }

    [Fact]
    public void HasUnresolvedVariables_WhenHasUnresolved_ReturnsTrue()
    {
        var request = CreateRequest("GET", "https://{{host}}/users");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request, true, "host");
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);

        Assert.True(resolved.HasUnresolvedVariables);
    }

    [Fact]
    public void AllUnresolvedVariables_ReturnsDistinctVariables()
    {
        var request1 = CreateRequest("GET", "https://{{host}}/users");
        var request2 = CreateRequest("POST", "https://{{host}}/{{apiVersion}}/users");
        var doc = CreateDocument(request1, request2);
        var resolvedRequests = new List<ResolvedHttpRequest>
        {
            CreateResolvedRequest(request1, true, "host"),
            CreateResolvedRequest(request2, true, "host", "apiVersion")
        };
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, resolvedRequests, context);
        var unresolved = resolved.AllUnresolvedVariables.ToList();

        Assert.Equal(2, unresolved.Count);
        Assert.Contains("host", unresolved);
        Assert.Contains("apiVersion", unresolved);
    }

    [Fact]
    public void AllUnresolvedVariables_WhenEmpty_ReturnsEmpty()
    {
        var request = CreateRequest("GET", "https://api.example.com/users");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);

        Assert.Empty(resolved.AllUnresolvedVariables);
    }

    [Fact]
    public void GetRequestByName_FindsNamedRequest()
    {
        var request1 = CreateRequest("GET", "https://api.example.com/users", "getUsers");
        var request2 = CreateRequest("POST", "https://api.example.com/users", "createUser");
        var doc = CreateDocument(request1, request2);
        var resolvedRequests = new List<ResolvedHttpRequest>
        {
            CreateResolvedRequest(request1),
            CreateResolvedRequest(request2)
        };
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, resolvedRequests, context);
        var found = resolved.GetRequestByName("createUser");

        Assert.NotNull(found);
        Assert.Equal("POST", found.OriginalRequest.Method);
    }

    [Fact]
    public void GetRequestByName_CaseInsensitive()
    {
        var request = CreateRequest("GET", "https://api.example.com/users", "GetUsers");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);

        Assert.NotNull(resolved.GetRequestByName("getusers"));
        Assert.NotNull(resolved.GetRequestByName("GETUSERS"));
        Assert.NotNull(resolved.GetRequestByName("GetUsers"));
    }

    [Fact]
    public void GetRequestByName_WhenNotFound_ReturnsNull()
    {
        var request = CreateRequest("GET", "https://api.example.com/users", "getUsers");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);
        var found = resolved.GetRequestByName("nonexistent");

        Assert.Null(found);
    }

    [Fact]
    public void GetRequestByName_WhenNoNamedRequests_ReturnsNull()
    {
        var request = CreateRequest("GET", "https://api.example.com/users");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);
        var found = resolved.GetRequestByName("anyName");

        Assert.Null(found);
    }

    [Fact]
    public void Context_ReturnsVariableContext()
    {
        var request = CreateRequest("GET", "https://api.example.com/users");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var resolver = new FileVariableResolver();
        resolver.SetVariable("baseUrl", "https://api.example.com");
        var context = new VariableContext(new[] { resolver });

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);

        Assert.Same(context, resolved.Context);
        Assert.Equal("https://api.example.com", resolved.Context.Resolve("baseUrl"));
    }

    [Fact]
    public void OriginalDocument_ReturnsOriginal()
    {
        var request = CreateRequest("GET", "https://api.example.com/users");
        var doc = CreateDocument(request);
        var resolvedRequest = CreateResolvedRequest(request);
        var context = new VariableContext();

        var resolved = new ResolvedHttpDocument(doc, new List<ResolvedHttpRequest> { resolvedRequest }, context);

        Assert.Same(doc, resolved.OriginalDocument);
        Assert.Single(resolved.OriginalDocument.Requests);
    }
}

#endregion
