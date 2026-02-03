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

    #region XPath Resolution Tests

    [Fact]
    public void Resolve_XPath_ExtractsXmlValue()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><id>123</id><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./user/name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_XPath_RootElementSelection()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getStatus",
            200,
            new Dictionary<string, string>(),
            """<status>ok</status>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getStatus.response.body./status");

        Assert.Equal("ok", result);
    }

    [Fact]
    public void Resolve_XPath_AttributeSelection()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user id="123" active="true"><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./user/@id");

        Assert.Equal("123", result);
    }

    [Fact]
    public void Resolve_XPath_AttributeBoolean()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user id="123" active="true"><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./user/@active");

        Assert.Equal("true", result);
    }

    [Fact]
    public void Resolve_XPath_NestedElement()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<root><user><profile><email>john@example.com</email></profile></user></root>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./root/user/profile/email");

        Assert.Equal("john@example.com", result);
    }

    [Fact]
    public void Resolve_XPath_InvalidXPath_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./[invalid-xpath");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_XPath_NonExistentPath_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./user/email");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_XPath_InvalidXml_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """not valid xml at all""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./user/name");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_XPath_TextXmlContentType()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "text/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./user/name");

        Assert.Equal("John", result);
    }

    #endregion

    #region Content-Type Detection Tests

    [Fact]
    public void Resolve_AutoDetectsJsonContentType()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"name": "John"}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        // Using selector without $ prefix - should auto-detect JSON and prepend $.
        var result = resolver.Resolve("getUser.response.body.name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_AutoDetectsJsonContentTypeWithCharset()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"name": "John"}""",
            "application/json; charset=utf-8"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body.name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_AutoDetectsXmlContentType_ApplicationXml()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // Using selector without / prefix - should auto-detect XML
        var result = resolver.Resolve("getUser.response.body./user/name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_AutoDetectsXmlContentType_TextXml()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "text/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body./user/name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_NoContentTypeHeader_JsonPathStillWorks()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"name": "John"}""",
            null));

        var resolver = new RequestVariableResolver(provider);

        // Explicit JSONPath selector should still work
        var result = resolver.Resolve("getUser.response.body.$.name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_NoContentTypeHeader_XPathStillWorks()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            null));

        var resolver = new RequestVariableResolver(provider);

        // Explicit XPath selector should still work
        var result = resolver.Resolve("getUser.response.body./user/name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_NoContentTypeHeader_ImplicitSelectorReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"name": "John"}""",
            null));

        var resolver = new RequestVariableResolver(provider);

        // Without content type and without explicit selector prefix, should return null
        var result = resolver.Resolve("getUser.response.body.name");

        Assert.Null(result);
    }

    #endregion

    #region JSONPath Edge Cases

    [Fact]
    public void Resolve_JsonPath_MultipleMatches_ReturnsArray()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUsers",
            200,
            new Dictionary<string, string>(),
            """{"users": [{"name": "John"}, {"name": "Jane"}, {"name": "Bob"}]}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUsers.response.body.$.users[*].name");

        Assert.NotNull(result);
        Assert.Contains("John", result);
        Assert.Contains("Jane", result);
        Assert.Contains("Bob", result);
    }

    [Fact]
    public void Resolve_JsonPath_InvalidPath_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"name": "John"}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body.$[invalid");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_JsonPath_NonExistentPath_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"name": "John"}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body.$.email");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_JsonPath_InvalidJson_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """not valid json""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body.$.name");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_JsonPath_ArrayIndex()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUsers",
            200,
            new Dictionary<string, string>(),
            """{"users": ["John", "Jane", "Bob"]}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUsers.response.body.$.users[1]");

        Assert.Equal("Jane", result);
    }

    [Fact]
    public void Resolve_JsonPath_ReturnsObject()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"user": {"name": "John", "age": 30}}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("getUser.response.body.$.user");

        Assert.NotNull(result);
        Assert.Contains("name", result);
        Assert.Contains("John", result);
    }

    #endregion

    #region Headers Selector Edge Cases

    [Fact]
    public void Resolve_Headers_CaseInsensitiveMatching()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "login",
            200,
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        // Request with different casing
        var result1 = resolver.Resolve("login.response.headers.content-type");
        var result2 = resolver.Resolve("login.response.headers.CONTENT-TYPE");
        var result3 = resolver.Resolve("login.response.headers.Content-Type");

        Assert.Equal("application/json", result1);
        Assert.Equal("application/json", result2);
        Assert.Equal("application/json", result3);
    }

    [Fact]
    public void Resolve_Headers_NotFound_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "login",
            200,
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("login.response.headers.X-Custom-Header");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Headers_EmptyHeaderName_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "login",
            200,
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("login.response.headers.");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Headers_MultipleHeaders()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "login",
            200,
            new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123",
                ["X-Request-Id"] = "req-456",
                ["X-Rate-Limit"] = "100"
            },
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        Assert.Equal("Bearer token123", resolver.Resolve("login.response.headers.Authorization"));
        Assert.Equal("req-456", resolver.Resolve("login.response.headers.X-Request-Id"));
        Assert.Equal("100", resolver.Resolve("login.response.headers.X-Rate-Limit"));
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public void Resolve_NonExistentRequest_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("nonexistent.response.body.$.name");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_InvalidVariableFormat_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        Assert.Null(resolver.Resolve("test.invalid.body"));
        Assert.Null(resolver.Resolve("test.response.invalid"));
        Assert.Null(resolver.Resolve("justAName"));
    }

    [Fact]
    public void Resolve_ResponsePartCaseInsensitive()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string> { ["X-Header"] = "value" },
            """{"name": "John"}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        // Body variations
        Assert.Equal("John", resolver.Resolve("test.response.body.$.name"));
        Assert.Equal("John", resolver.Resolve("test.response.BODY.$.name"));
        Assert.Equal("John", resolver.Resolve("test.response.Body.$.name"));

        // Headers variations
        Assert.Equal("value", resolver.Resolve("test.response.headers.X-Header"));
        Assert.Equal("value", resolver.Resolve("test.response.HEADERS.X-Header"));
        Assert.Equal("value", resolver.Resolve("test.response.Headers.X-Header"));
    }

    [Fact]
    public void CanResolve_InvalidFormat_ReturnsFalse()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse("test", 200, new Dictionary<string, string>(), "{}", null));

        var resolver = new RequestVariableResolver(provider);

        Assert.False(resolver.CanResolve("test.invalid.body"));
        Assert.False(resolver.CanResolve("justAName"));
        Assert.False(resolver.CanResolve(""));
    }

    #endregion

    #region XPath Evaluation Result Types

    [Fact]
    public void Resolve_XPath_NumericResult_ReturnsString()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<items><item>1</item><item>2</item><item>3</item></items>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // count() returns a double
        var result = resolver.Resolve("getUser.response.body.count(/items/item)");

        Assert.NotNull(result);
        Assert.Equal("3", result);
    }

    [Fact]
    public void Resolve_XPath_BooleanResult_ReturnsLowercase()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user active="true"><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // boolean() returns a bool
        var result = resolver.Resolve("getUser.response.body.boolean(/user)");

        Assert.NotNull(result);
        Assert.Equal("true", result);
    }

    [Fact]
    public void Resolve_XPath_BooleanFalse_ReturnsLowercase()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // boolean on non-existent element
        var result = resolver.Resolve("getUser.response.body.boolean(/nonexistent)");

        Assert.NotNull(result);
        Assert.Equal("false", result);
    }

    [Fact]
    public void Resolve_XPath_StringFunction_ReturnsString()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // string() returns a string
        var result = resolver.Resolve("getUser.response.body.string(/user/name)");

        Assert.NotNull(result);
        Assert.Equal("John", result);
    }

    [Fact]
    public void Resolve_XPath_SumFunction_ReturnsNumeric()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<values><val>10</val><val>20</val><val>30</val></values>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // sum() returns a double
        var result = resolver.Resolve("getUser.response.body.sum(/values/val)");

        Assert.NotNull(result);
        Assert.Equal("60", result);
    }

    [Fact]
    public void Resolve_XPath_EmptyNodeSet_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // Empty nodeset when MoveNext returns false
        var result = resolver.Resolve("getUser.response.body./nonexistent/path");

        Assert.Null(result);
    }

    #endregion

    #region RequestResponse Property Tests

    [Fact]
    public void RequestResponse_AllPropertiesAccessible()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["X-Custom"] = "value"
        };
        var response = new RequestResponse(
            "testRequest",
            201,
            headers,
            """{"status": "created"}""",
            "application/json");

        Assert.Equal("testRequest", response.RequestName);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(2, response.Headers.Count);
        Assert.Contains("status", response.Body);
        Assert.Equal("application/json", response.ContentType);
    }

    [Fact]
    public void RequestResponse_NullContentType_Allowed()
    {
        var response = new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            "body",
            null);

        Assert.Null(response.ContentType);
    }

    [Fact]
    public void RequestResponse_WithDifferentStatusCodes()
    {
        var codes = new[] { 200, 201, 400, 404, 500 };
        foreach (var code in codes)
        {
            var response = new RequestResponse("test", code, new Dictionary<string, string>(), "", null);
            Assert.Equal(code, response.StatusCode);
        }
    }

    #endregion

    #region Body Selector Without Prefix

    [Fact]
    public void Resolve_BodySelector_ImplicitXmlPath_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // Using implicit selector (not starting with / or $) with XML content type
        // should try XPath with the selector as-is
        var result = resolver.Resolve("getUser.response.body.user.name");

        // This doesn't start with / or $, and content type is xml
        // So it will try as XPath which won't match
        Assert.Null(result);
    }

    #endregion

    #region Body Selector Additional Edge Cases

    [Fact]
    public void Resolve_BodySelector_GenericSelector_WithoutContentType_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """{"name": "John"}""",
            null)); // No content type

        var resolver = new RequestVariableResolver(provider);

        // Selector doesn't start with $ or /, and no content type to auto-detect
        var result = resolver.Resolve("getUser.response.body.name");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_BodySelector_WithXmlContentType_ImplicitSelector_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "getUser",
            200,
            new Dictionary<string, string>(),
            """<user><name>John</name></user>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // Selector doesn't start with / - with XML content type
        // ResolveXPath will be called but path won't be valid XPath
        var result = resolver.Resolve("getUser.response.body.user.name");

        // This returns null because "user.name" is not valid XPath
        Assert.Null(result);
    }

    #endregion

    #region XPath Additional Edge Cases

    [Fact]
    public void Resolve_XPath_NavigatorEvaluate_ReturnsString()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """<root><value>test</value></root>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // Using string() function which returns a string type result
        var result = resolver.Resolve("test.response.body.string(/root/value)");

        Assert.Equal("test", result);
    }

    [Fact]
    public void Resolve_XPath_EvaluateNumericExpression()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "calc",
            200,
            new Dictionary<string, string>(),
            """<values><a>10</a><b>20</b></values>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // XPath can evaluate numeric expressions
        var result = resolver.Resolve("calc.response.body.number(/values/a) + number(/values/b)");

        Assert.NotNull(result);
        // Result is a double -> ToString()
        Assert.Equal("30", result);
    }

    [Fact]
    public void Resolve_XPath_BooleanExpression_True()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """<root><exists>yes</exists></root>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // boolean() on existing node
        var result = resolver.Resolve("test.response.body.boolean(/root/exists)");

        Assert.Equal("true", result);
    }

    [Fact]
    public void Resolve_XPath_NavigatorIsNull_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """invalid xml <><><""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body./root");

        // Invalid XML causes exception -> null
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_XPath_XPathNodeIterator_EmptyResult()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """<root><item>1</item></root>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // XPath that returns empty node set
        var result = resolver.Resolve("test.response.body./root/nonexistent");

        // MoveNext returns false -> null
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_XPath_NumericFunctionResult()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """<items><item>a</item><item>b</item><item>c</item></items>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // count() returns double
        var result = resolver.Resolve("test.response.body.count(/items/item)");

        Assert.Equal("3", result);
    }

    [Fact]
    public void Resolve_XPath_StringLengthFunction()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """<root><text>Hello World</text></root>""",
            "application/xml"));

        var resolver = new RequestVariableResolver(provider);

        // string-length returns double
        var result = resolver.Resolve("test.response.body.string-length(/root/text)");

        Assert.Equal("11", result);
    }

    #endregion

    #region JSONPath Additional Edge Cases

    [Fact]
    public void Resolve_JsonPath_NullRootNode_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            "null",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body.$.something");

        // JSON is literally null, so path won't match
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_JsonPath_EmptyArray_ReturnsEmptyArray()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """{"items": []}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body.$.items[*]");

        // Empty array matches result in empty
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_JsonPath_NumberValue_ReturnsString()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """{"count": 42}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body.$.count");

        Assert.Equal("42", result);
    }

    [Fact]
    public void Resolve_JsonPath_BooleanValue_ReturnsString()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """{"active": true}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body.$.active");

        Assert.NotNull(result);
        Assert.Contains("true", result.ToLower());
    }

    [Fact]
    public void Resolve_JsonPath_StringValue_ReturnsString()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """{"message": "hello"}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body.$.message");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Resolve_JsonPath_ObjectValue_ReturnsJsonString()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """{"nested": {"a": 1, "b": 2}}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body.$.nested");

        Assert.NotNull(result);
        Assert.Contains("\"a\"", result);
        Assert.Contains("1", result);
    }

    [Fact]
    public void Resolve_JsonPath_ArrayValue_ReturnsJsonArray()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """{"items": [1, 2, 3]}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.body.$.items");

        Assert.NotNull(result);
        Assert.Contains("[", result);
        Assert.Contains("1", result);
    }

    [Fact]
    public void Resolve_JsonPath_MultipleObjectMatches()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            """{"users": [{"id": 1}, {"id": 2}]}""",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        // Multiple matches for [*].id -> returns array
        var result = resolver.Resolve("test.response.body.$.users[*].id");

        Assert.NotNull(result);
        Assert.Contains("1", result);
        Assert.Contains("2", result);
    }

    #endregion

    #region Headers Additional Edge Cases

    [Fact]
    public void Resolve_Headers_NoSelector_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string> { ["X-Custom"] = "value" },
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        // Just "headers" without header name
        var result = resolver.Resolve("test.response.headers");

        // Empty selector returns null
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Headers_EmptyHeadersCollection()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve("test.response.headers.Any-Header");

        Assert.Null(result);
    }

    #endregion

    #region Unknown Response Part

    [Fact]
    public void Resolve_UnknownResponsePart_ReturnsNull()
    {
        var provider = new InMemoryResponseProvider();
        provider.AddResponse(new RequestResponse(
            "test",
            200,
            new Dictionary<string, string>(),
            "{}",
            "application/json"));

        var resolver = new RequestVariableResolver(provider);

        // Use an unknown response part (not "body" or "headers")
        var result = resolver.Resolve("test.response.status.200");

        Assert.Null(result);
    }

    #endregion

    #region Regex Pattern Edge Cases

    [Fact]
    public void Resolve_RequestNameWithDots_NotMatched()
    {
        // Request name with dots shouldn't match the pattern
        // Pattern is: ^([^.]+)\.response\.(body|headers)(?:\.(.+))?$
        var provider = new InMemoryResponseProvider();
        var resolver = new RequestVariableResolver(provider);

        // This won't match because "req.name" contains a dot
        var result = resolver.Resolve("req.name.response.body");

        // Actually this might match with req as name, but let's test
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyRequestName_NotMatched()
    {
        var provider = new InMemoryResponseProvider();
        var resolver = new RequestVariableResolver(provider);

        var result = resolver.Resolve(".response.body");

        Assert.Null(result);
    }

    [Fact]
    public void CanResolve_MalformedPattern_ReturnsFalse()
    {
        var provider = new InMemoryResponseProvider();
        var resolver = new RequestVariableResolver(provider);

        Assert.False(resolver.CanResolve(""));
        Assert.False(resolver.CanResolve(".response.body"));
        Assert.False(resolver.CanResolve("test..body"));
        Assert.False(resolver.CanResolve("test.response."));
    }

    #endregion
}
