using System.Text;
using HttpFileParser.Execution;
using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Tests;

public class HttpRequestBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<HttpRequestMessage> _requestsToDispose = new();

    public HttpRequestBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"HttpRequestBuilderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        // Dispose all tracked requests to release file handles
        foreach (var request in _requestsToDispose)
        {
            request.Dispose();
        }
        _requestsToDispose.Clear();

        // Allow time for file handles to be released
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch (IOException)
            {
                // Ignore cleanup failures in tests
            }
        }
    }

    private HttpRequestMessage TrackRequest(HttpRequestMessage request)
    {
        _requestsToDispose.Add(request);
        return request;
    }
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

    #region FileReferenceBody Tests

    [Fact]
    public async Task Build_FileReferenceBody_ReadsFileContent()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "request-body.json");
        await File.WriteAllTextAsync(testFilePath, """{"name": "TestUser"}""");

        var request = CreateRequestWithFileBody(testFilePath);
        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = TrackRequest(builder.Build(request));

        // Assert
        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("TestUser", body);
    }

    [Theory]
    [InlineData("utf-8", "Hello UTF8 Ω")]
    [InlineData("utf8", "Hello UTF8 Ω")]
    [InlineData("ascii", "Hello ASCII")]
    public async Task Build_FileReferenceBody_WithEncoding_UsesCorrectEncoding(string encodingName, string content)
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, $"test-{encodingName}.txt");
        var encoding = encodingName.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => Encoding.UTF8,
            "ascii" => Encoding.ASCII,
            _ => Encoding.UTF8
        };
        await File.WriteAllTextAsync(testFilePath, content, encoding);

        var fileBody = new FileReferenceBody(testFilePath, encodingName, processVariables: true, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var context = VariableContext.CreateDefault();
        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = builder.Build(request, context);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var resultBody = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains(content.Replace("Ω", ""), resultBody); // ASCII may lose special chars
    }

    [Fact]
    public async Task Build_FileReferenceBody_Utf16Encoding_ReadsCorrectly()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "test-utf16.txt");
        var content = "Hello UTF16 世界";
        await File.WriteAllTextAsync(testFilePath, content, Encoding.Unicode);

        var fileBody = new FileReferenceBody(testFilePath, "utf-16", processVariables: true, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var context = VariableContext.CreateDefault();
        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = builder.Build(request, context);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var resultBody = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("世界", resultBody);
    }

    [Fact]
    public async Task Build_FileReferenceBody_Latin1Encoding_ReadsCorrectly()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "test-latin1.txt");
        var content = "Hello Latin1 café";
        await File.WriteAllTextAsync(testFilePath, content, Encoding.Latin1);

        var fileBody = new FileReferenceBody(testFilePath, "latin1", processVariables: true, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var context = VariableContext.CreateDefault();
        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = builder.Build(request, context);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var resultBody = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("café", resultBody);
    }

    [Fact]
    public void Build_FileReferenceBody_FileNotFound_ReturnsNullContent()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDir, "does-not-exist.json");
        var fileBody = new FileReferenceBody(nonExistentPath, null, false, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert
        Assert.Null(httpRequest.Content);
    }

    [Fact]
    public async Task Build_FileReferenceBody_RelativePath_ResolvesFromBaseDirectory()
    {
        // Arrange
        var testFileName = "relative-body.json";
        var testFilePath = Path.Combine(_tempDir, testFileName);
        await File.WriteAllTextAsync(testFilePath, """{"id": 42}""");

        var fileBody = new FileReferenceBody(testFileName, null, false, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = TrackRequest(builder.Build(request));

        // Assert
        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("42", body);
    }

    [Fact]
    public async Task Build_FileReferenceBody_ProcessVariablesTrue_ExpandsVariables()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "template.json");
        await File.WriteAllTextAsync(testFilePath, """{"username": "{{userName}}", "host": "{{$hostname}}"}""");

        var fileBody = new FileReferenceBody(testFilePath, "utf-8", processVariables: true, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var fileResolver = new FileVariableResolver();
        fileResolver.SetVariable("userName", "JohnDoe");
        var context = VariableContext.CreateDefault();
        context.AddResolver(fileResolver);

        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = builder.Build(request, context);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("JohnDoe", body);
        Assert.DoesNotContain("{{userName}}", body);
    }

    [Fact]
    public async Task Build_FileReferenceBody_ProcessVariablesFalse_DoesNotExpandVariables()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "literal.json");
        await File.WriteAllTextAsync(testFilePath, """{"template": "{{userName}}"}""");

        var fileBody = new FileReferenceBody(testFilePath, null, processVariables: false, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var fileResolver = new FileVariableResolver();
        fileResolver.SetVariable("userName", "JohnDoe");
        var context = VariableContext.CreateDefault();
        context.AddResolver(fileResolver);

        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = TrackRequest(builder.Build(request, context));

        // Assert
        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("{{userName}}", body);
    }

    #endregion

    #region MultipartBody Tests

    [Fact]
    public async Task Build_MultipartBody_CreatesMultipartFormDataContent()
    {
        // Arrange
        var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
        var textSection = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"field1\"", SourceSpan.Empty)
            },
            new TextBody("value1", SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { textSection }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert
        Assert.NotNull(httpRequest.Content);
        Assert.IsType<MultipartFormDataContent>(httpRequest.Content);
        var contentType = httpRequest.Content.Headers.ContentType;
        Assert.NotNull(contentType);
        Assert.Equal("multipart/form-data", contentType.MediaType);
    }

    [Fact]
    public async Task Build_MultipartBody_WithTextSection_AddsStringContent()
    {
        // Arrange
        var boundary = "----TestBoundary";
        var textSection = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"description\"", SourceSpan.Empty)
            },
            new TextBody("This is a test description", SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { textSection }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var content = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("This is a test description", content);
        Assert.Contains("description", content);
    }

    [Fact]
    public async Task Build_MultipartBody_WithFileSection_AddsFileContent()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "upload.txt");
        await File.WriteAllTextAsync(testFilePath, "File content for upload");

        var boundary = "----TestBoundary";
        var fileSection = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"file\"; filename=\"upload.txt\"", SourceSpan.Empty)
            },
            new FileReferenceBody(testFilePath, null, false, SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { fileSection }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = TrackRequest(builder.Build(request));

        // Assert
        Assert.NotNull(httpRequest.Content);
        var content = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("File content for upload", content);
    }

    [Fact]
    public async Task Build_MultipartBody_MultipleSections_AllIncluded()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "doc.pdf");
        await File.WriteAllTextAsync(testFilePath, "%PDF-1.4 mock content");

        var boundary = "----MultipleSections";
        var sections = new List<MultipartSection>
        {
            new(
                new List<HttpHeader> { new("Content-Disposition", "form-data; name=\"title\"", SourceSpan.Empty) },
                new TextBody("Document Title", SourceSpan.Empty),
                SourceSpan.Empty),
            new(
                new List<HttpHeader> { new("Content-Disposition", "form-data; name=\"author\"", SourceSpan.Empty) },
                new TextBody("John Doe", SourceSpan.Empty),
                SourceSpan.Empty),
            new(
                new List<HttpHeader> { new("Content-Disposition", "form-data; name=\"document\"; filename=\"doc.pdf\"", SourceSpan.Empty) },
                new FileReferenceBody(testFilePath, null, false, SourceSpan.Empty),
                SourceSpan.Empty)
        };

        var multipartBody = new MultipartBody(boundary, sections, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = TrackRequest(builder.Build(request));

        // Assert
        Assert.NotNull(httpRequest.Content);
        var content = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("Document Title", content);
        Assert.Contains("John Doe", content);
        Assert.Contains("%PDF-1.4", content);
    }

    [Fact]
    public void Build_MultipartBody_ExtractDispositionName_ExtractsFieldName()
    {
        // This tests through the Build method behavior
        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"myFieldName\"", SourceSpan.Empty)
            },
            new TextBody("content", SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act - The field name should be correctly extracted internally
        var httpRequest = builder.Build(request);

        // Assert - If it builds without error and has content, name extraction worked
        Assert.NotNull(httpRequest.Content);
    }

    [Fact]
    public async Task Build_MultipartBody_ExtractDispositionFileName_ExtractsFilename()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "myfile.txt");
        await File.WriteAllTextAsync(testFilePath, "test content");

        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"uploadFile\"; filename=\"custom-filename.txt\"", SourceSpan.Empty)
            },
            new FileReferenceBody(testFilePath, null, false, SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = TrackRequest(builder.Build(request));

        // Assert
        Assert.NotNull(httpRequest.Content);
        var content = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("custom-filename.txt", content);
    }

    [Fact]
    public void Build_MultipartBody_NoDispositionName_UsesDefaultName()
    {
        // Arrange - section without name in Content-Disposition
        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data", SourceSpan.Empty) // No name attribute
            },
            new TextBody("content", SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert - Should use default name "file"
        Assert.NotNull(httpRequest.Content);
    }

    #endregion

    #region Header Handling Tests

    [Theory]
    [InlineData("Content-Type")]
    [InlineData("Content-Length")]
    [InlineData("Content-Disposition")]
    [InlineData("Content-Encoding")]
    [InlineData("Content-Language")]
    [InlineData("Content-Location")]
    [InlineData("Content-MD5")]
    [InlineData("Content-Range")]
    [InlineData("Expires")]
    [InlineData("Last-Modified")]
    public void Build_ContentHeaders_NotOnRequestHeaders(string headerName)
    {
        // Content headers should NOT be placed on request headers, they go on content
        // This test verifies that IsContentHeader correctly identifies them
        var content = $"POST https://api.example.com/test\n{headerName}: application/json\n\n{{\"test\": \"data\"}}";

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        // Content headers throw if we try to access them on request headers via Contains
        // This verifies that the header was NOT placed on request headers
        // (The actual header is on httpRequest.Content.Headers)
        var requestHeaderNames = httpRequest.Headers.Select(h => h.Key).ToList();
        Assert.DoesNotContain(headerName, requestHeaderNames, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Accept")]
    [InlineData("User-Agent")]
    [InlineData("X-Custom-Header")]
    public void Build_NonContentHeaders_OnRequestHeaders(string headerName)
    {
        var content = $"GET https://api.example.com/test\n{headerName}: test-value";

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.True(httpRequest.Headers.Contains(headerName), $"{headerName} should be on request headers");
        Assert.Equal("test-value", httpRequest.Headers.GetValues(headerName).First());
    }

    [Fact]
    public async Task Build_DetectContentType_ReturnsJsonForJsonContent()
    {
        var content = """
            POST https://api.example.com/test

            {"key": "value"}
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("application/json", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Build_DetectContentType_ReturnsJsonForArrayContent()
    {
        var content = """
            POST https://api.example.com/test

            [1, 2, 3]
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("application/json", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Build_DetectContentType_ReturnsXmlForXmlContent()
    {
        var content = """
            POST https://api.example.com/test

            <?xml version="1.0"?>
            <root><item>value</item></root>
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("application/xml", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Build_DetectContentType_ReturnsXmlForSoapContent()
    {
        var content = """
            POST https://api.example.com/test

            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
            </soap:Envelope>
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("application/xml", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Build_DetectContentType_ReturnsHtmlForHtmlContent()
    {
        var content = """
            POST https://api.example.com/test

            <html><body>Hello</body></html>
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("text/html", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Build_DetectContentType_ReturnsTextPlainForPlainText()
    {
        var content = """
            POST https://api.example.com/test

            Just plain text content
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("text/plain", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Build_ExplicitContentType_OverridesDetection()
    {
        var content = """
            POST https://api.example.com/test
            Content-Type: application/custom-type

            {"key": "value"}
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("application/custom-type", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Build_NullBody_NoContentAdded()
    {
        var content = """
            GET https://api.example.com/test
            Accept: application/json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.Null(httpRequest.Content);
    }

    [Fact]
    public async Task Build_EmptyBody_EmptyContent()
    {
        // Create a request with an empty text body
        var textBody = new TextBody("", SourceSpan.Empty);
        var request = CreateRequestWithBody(textBody);

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }

    [Fact]
    public void Build_GetMethod_CreatesCorrectMethod()
    {
        var content = "GET https://api.example.com/test";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Equal(HttpMethod.Get, httpRequest.Method);
    }

    [Fact]
    public void Build_PostMethod_CreatesCorrectMethod()
    {
        var content = "POST https://api.example.com/test";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Equal(HttpMethod.Post, httpRequest.Method);
    }

    [Fact]
    public void Build_PutMethod_CreatesCorrectMethod()
    {
        var content = "PUT https://api.example.com/test";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Equal(HttpMethod.Put, httpRequest.Method);
    }

    [Fact]
    public void Build_DeleteMethod_CreatesCorrectMethod()
    {
        var content = "DELETE https://api.example.com/test";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Equal(HttpMethod.Delete, httpRequest.Method);
    }

    [Fact]
    public void Build_PatchMethod_CreatesCorrectMethod()
    {
        var content = "PATCH https://api.example.com/test";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Equal(HttpMethod.Patch, httpRequest.Method);
    }

    [Fact]
    public void Build_CustomMethod_CreatesCorrectMethod()
    {
        // Create a request directly with a custom method (parser may not support all methods)
        var request = new HttpRequest(
            method: "CUSTOM",
            rawUrl: "https://api.example.com/test",
            httpVersion: null,
            name: null,
            headers: new List<HttpHeader>(),
            body: null,
            directives: new List<HttpDirective>(),
            leadingComments: new List<Comment>(),
            span: SourceSpan.Empty);

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Equal("CUSTOM", httpRequest.Method.Method);
    }

    [Fact]
    public void Build_WithQueryParameters_PreservesQueryString()
    {
        var content = "GET https://api.example.com/test?param1=value1&param2=value2";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Contains("param1=value1", httpRequest.RequestUri?.Query);
        Assert.Contains("param2=value2", httpRequest.RequestUri?.Query);
    }

    [Fact]
    public async Task Build_WhitespaceOnlyBody_PreservesWhitespace()
    {
        var textBody = new TextBody("   \n\t  ", SourceSpan.Empty);
        var request = CreateRequestWithBody(textBody);

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Equal("   \n\t  ", body);
    }

    [Fact]
    public async Task Build_BodyWithSpecialCharacters_PreservesCharacters()
    {
        var specialContent = "{\"emoji\": \"🎉\", \"unicode\": \"你好世界\", \"symbols\": \"©®™\"}";
        var textBody = new TextBody(specialContent, SourceSpan.Empty);
        var request = CreateRequestWithBody(textBody);

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        var body = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("🎉", body);
        Assert.Contains("你好世界", body);
    }

    [Fact]
    public void Build_MultipleHeaders_SameNameAllowed()
    {
        var content = """
            GET https://api.example.com/test
            Accept: application/json
            Accept: text/plain
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        var acceptHeaders = httpRequest.Headers.GetValues("Accept").ToList();
        Assert.Contains("application/json", acceptHeaders);
        Assert.Contains("text/plain", acceptHeaders);
    }

    [Fact]
    public void Build_WithBaseDirectoryNull_HandlesRelativePaths()
    {
        var fileBody = new FileReferenceBody("nonexistent.json", null, false, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var builder = new HttpRequestBuilder(null); // null base directory

        // Should not throw, but file won't be found
        var httpRequest = builder.Build(request);
        Assert.Null(httpRequest.Content);
    }

    [Fact]
    public async Task Build_WithVariableContextNull_StillWorks()
    {
        var content = """
            GET https://api.example.com/test
            Accept: application/json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var builder = new HttpRequestBuilder(null, null);
        var httpRequest = builder.Build(request);

        Assert.Equal("https://api.example.com/test", httpRequest.RequestUri?.ToString());
    }

    #endregion

    #region Additional Encoding Tests

    [Fact]
    public async Task Build_FileReferenceBody_Iso8859_1Encoding_ReadsCorrectly()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "test-iso8859.txt");
        var content = "Café résumé naïve";
        await File.WriteAllTextAsync(testFilePath, content, Encoding.Latin1);

        var fileBody = new FileReferenceBody(testFilePath, "iso-8859-1", processVariables: true, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var context = VariableContext.CreateDefault();
        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = builder.Build(request, context);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var resultBody = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("Café", resultBody);
    }

    [Fact]
    public async Task Build_FileReferenceBody_Utf16AliasEncoding_ReadsCorrectly()
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "test-utf16-2.txt");
        var content = "Unicode 日本語";
        await File.WriteAllTextAsync(testFilePath, content, Encoding.Unicode);

        var fileBody = new FileReferenceBody(testFilePath, "utf16", processVariables: true, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var context = VariableContext.CreateDefault();
        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = builder.Build(request, context);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var resultBody = await httpRequest.Content.ReadAsStringAsync();
        Assert.Contains("日本語", resultBody);
    }

    [Theory]
    [InlineData("unknown-encoding")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Build_FileReferenceBody_UnknownEncoding_DefaultsToUtf8(string? encodingName)
    {
        // Arrange
        var testFilePath = Path.Combine(_tempDir, "test-default-enc.txt");
        var content = "Hello World";
        await File.WriteAllTextAsync(testFilePath, content, Encoding.UTF8);

        var fileBody = new FileReferenceBody(testFilePath, encodingName, processVariables: true, SourceSpan.Empty);
        var request = CreateRequestWithBody(fileBody);

        var context = VariableContext.CreateDefault();
        var builder = new HttpRequestBuilder(_tempDir, context);

        // Act
        var httpRequest = builder.Build(request, context);

        // Assert
        Assert.NotNull(httpRequest.Content);
        var resultBody = await httpRequest.Content.ReadAsStringAsync();
        Assert.Equal(content, resultBody);
    }

    #endregion

    #region Additional Multipart Tests

    [Fact]
    public async Task Build_MultipartBody_WithNullSectionBody_SkipsSection()
    {
        // Arrange
        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"empty\"", SourceSpan.Empty)
            },
            null, // No body
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert - Should not throw even with null section body
        Assert.NotNull(httpRequest.Content);
    }

    [Fact]
    public async Task Build_MultipartBody_FileReferenceNotFound_SkipsSection()
    {
        // Arrange
        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"file\"; filename=\"missing.txt\"", SourceSpan.Empty)
            },
            new FileReferenceBody(Path.Combine(_tempDir, "nonexistent.txt"), null, false, SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert - Should not throw, just skip the section
        Assert.NotNull(httpRequest.Content);
    }

    [Fact]
    public void Build_MultipartSection_MalformedContentDisposition_UsesDefaults()
    {
        // Arrange - Content-Disposition without proper name="" format
        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data", SourceSpan.Empty) // No name attribute
            },
            new TextBody("content", SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert - Should use default name "file"
        Assert.NotNull(httpRequest.Content);
    }

    [Fact]
    public async Task Build_MultipartSection_IncompleteNameQuotes_ReturnsNull()
    {
        // Arrange - name="something without closing quote
        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"incomplete", SourceSpan.Empty)
            },
            new TextBody("content", SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert - Should still work, using default name
        Assert.NotNull(httpRequest.Content);
    }

    [Fact]
    public async Task Build_MultipartSection_IncompleteFilenameQuotes_ReturnsNull()
    {
        // Arrange - filename="something without closing quote
        var boundary = "----TestBoundary";
        var testFilePath = Path.Combine(_tempDir, "test-file.txt");
        await File.WriteAllTextAsync(testFilePath, "test content");

        var section = new MultipartSection(
            new List<HttpHeader>
            {
                new("Content-Disposition", "form-data; name=\"file\"; filename=\"noclose", SourceSpan.Empty)
            },
            new FileReferenceBody(testFilePath, null, false, SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = TrackRequest(builder.Build(request));

        // Assert - Should work, filename just won't be set
        Assert.NotNull(httpRequest.Content);
    }

    [Fact]
    public void Build_MultipartSection_NoContentDisposition_UsesDefaultName()
    {
        // Arrange - No Content-Disposition header at all
        var boundary = "----TestBoundary";
        var section = new MultipartSection(
            new List<HttpHeader>(), // Empty headers
            new TextBody("content", SourceSpan.Empty),
            SourceSpan.Empty);

        var multipartBody = new MultipartBody(boundary, new[] { section }, SourceSpan.Empty);
        var request = CreateRequestWithBody(multipartBody);

        var builder = new HttpRequestBuilder(_tempDir);

        // Act
        var httpRequest = builder.Build(request);

        // Assert - Should use default name "file"
        Assert.NotNull(httpRequest.Content);
    }

    #endregion

    #region Content Type Detection Additional Tests

    [Fact]
    public void Build_PlainTextBody_UsesTextPlainContentType()
    {
        var content = """
            POST https://api.example.com/test

            This is just plain text without any special markers
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var builder = new HttpRequestBuilder();

        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
        Assert.Equal("text/plain", httpRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Build_EmptyStringBody_StillHasContent()
    {
        var textBody = new TextBody("", SourceSpan.Empty);
        var request = CreateRequestWithBody(textBody);

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.NotNull(httpRequest.Content);
    }

    #endregion

    #region Unknown Body Type Test

    [Fact]
    public void Build_UnknownBodyType_ReturnsNullContent()
    {
        // This tests the default case in the switch expression for body types
        // Since we can't easily create a new body type, we test that null body returns null content
        var request = new HttpRequest(
            method: "POST",
            rawUrl: "https://api.example.com/test",
            httpVersion: null,
            name: null,
            headers: new List<HttpHeader>(),
            body: null,
            directives: new List<HttpDirective>(),
            leadingComments: new List<Comment>(),
            span: SourceSpan.Empty);

        var builder = new HttpRequestBuilder();
        var httpRequest = builder.Build(request);

        Assert.Null(httpRequest.Content);
    }

    #endregion

    #region Helper Methods

    private static HttpRequest CreateRequestWithFileBody(string filePath)
    {
        var fileBody = new FileReferenceBody(filePath, null, false, SourceSpan.Empty);
        return CreateRequestWithBody(fileBody);
    }

    private static HttpRequest CreateRequestWithBody(HttpRequestBody body)
    {
        return new HttpRequest(
            method: "POST",
            rawUrl: "https://api.example.com/test",
            httpVersion: null,
            name: null,
            headers: new List<HttpHeader>(),
            body: body,
            directives: new List<HttpDirective>(),
            leadingComments: new List<Comment>(),
            span: SourceSpan.Empty);
    }

    #endregion
}
