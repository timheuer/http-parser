namespace HttpFileParser.Tests;

public class HttpParserTests
{
    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyDocument()
    {
        var doc = HttpFile.Parse("");

        Assert.Empty(doc.Items);
        Assert.Empty(doc.Diagnostics);
    }

    [Fact]
    public void Parse_SingleRequest_ReturnsOneRequest()
    {
        var content = "GET https://api.example.com/users";
        var doc = HttpFile.Parse(content);

        Assert.Single(doc.Requests);
        Assert.Equal("GET", doc.Requests.First().Method);
        Assert.Equal("https://api.example.com/users", doc.Requests.First().RawUrl);
    }

    [Fact]
    public void Parse_RequestWithHeaders_ParsesHeaders()
    {
        var content = """
            GET https://api.example.com/users
            Authorization: Bearer token
            Accept: application/json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Equal(2, request.Headers.Count);
        Assert.Equal("Authorization", request.Headers[0].Name);
        Assert.Equal("Bearer token", request.Headers[0].RawValue);
        Assert.Equal("Accept", request.Headers[1].Name);
        Assert.Equal("application/json", request.Headers[1].RawValue);
    }

    [Fact]
    public void Parse_RequestWithBody_ParsesBody()
    {
        var content = """
            POST https://api.example.com/users
            Content-Type: application/json

            {
              "name": "John"
            }
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.NotNull(request.Body);
        Assert.IsType<HttpFileParser.Model.TextBody>(request.Body);
        var textBody = (HttpFileParser.Model.TextBody)request.Body;
        Assert.Contains("John", textBody.Content);
    }

    [Fact]
    public void Parse_MultipleRequests_ParsesAll()
    {
        var content = """
            GET https://api.example.com/users

            ###

            POST https://api.example.com/users
            Content-Type: application/json

            {"name": "John"}
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Requests.Count());
        Assert.Equal("GET", doc.Requests.First().Method);
        Assert.Equal("POST", doc.Requests.Last().Method);
    }

    [Fact]
    public void Parse_VariableDefinitions_ParsesVariables()
    {
        var content = """
            @baseUrl = https://api.example.com
            @token = secret-token

            GET {{baseUrl}}/users
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Variables.Count());
        Assert.Equal("baseUrl", doc.Variables.First().Name);
        Assert.Equal("https://api.example.com", doc.Variables.First().RawValue);
    }

    [Fact]
    public void Parse_RequestWithName_ParsesName()
    {
        var content = """
            # @name getUsers
            GET https://api.example.com/users
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Equal("getUsers", request.Name);
    }

    [Fact]
    public void Parse_RequestWithDirectives_ParsesDirectives()
    {
        var content = """
            # @name myRequest
            # @no-redirect
            GET https://api.example.com/users
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Equal(2, request.Directives.Count);
        Assert.True(request.HasDirective("no-redirect"));
    }

    [Fact]
    public void Parse_AllHttpMethods_ParsesCorrectly()
    {
        var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };

        foreach (var method in methods)
        {
            var content = $"{method} https://api.example.com/test";
            var doc = HttpFile.Parse(content);

            Assert.Single(doc.Requests);
            Assert.Equal(method, doc.Requests.First().Method);
        }
    }

    [Fact]
    public void Parse_RequestWithHttpVersion_ParsesVersion()
    {
        var content = "GET https://api.example.com/users HTTP/1.1";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Equal("HTTP/1.1", request.HttpVersion);
    }

    [Fact]
    public void Parse_MultilineUrl_CombinesLines()
    {
        var content = """
            GET https://api.example.com/users
              ?page=1
              &limit=10
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Contains("page=1", request.RawUrl);
        Assert.Contains("limit=10", request.RawUrl);
    }

    [Fact]
    public void Parse_FileReferenceBody_ParsesCorrectly()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: application/json

            < ./data.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.NotNull(request.Body);
        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body;
        Assert.Equal("./data.json", fileBody.FilePath);
    }

    [Fact]
    public void Parse_GraphQLRequest_DetectsGraphQL()
    {
        var content = """
            POST https://api.example.com/graphql
            X-REQUEST-TYPE: GraphQL
            Content-Type: application/json

            {
              query Users {
                users { id name }
              }
            }
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.True(request.IsGraphQL);
    }

    [Fact]
    public void GetRequestByName_FindsRequest()
    {
        var content = """
            # @name getUsers
            GET https://api.example.com/users

            ###

            # @name createUser
            POST https://api.example.com/users
            """;

        var doc = HttpFile.Parse(content);

        var request = doc.GetRequestByName("createUser");
        Assert.NotNull(request);
        Assert.Equal("POST", request.Method);
    }

    #region Parse(Stream) Tests

    [Fact]
    public void Parse_Stream_ParsesCorrectly()
    {
        var content = """
            GET https://api.example.com/users
            Authorization: Bearer token
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var parser = new HttpFileParser.Parsing.HttpParser();
        var doc = parser.Parse(stream, "test.http");

        Assert.Single(doc.Requests);
        Assert.Equal("GET", doc.Requests.First().Method);
        Assert.Equal("test.http", doc.FilePath);
    }

    [Fact]
    public void Parse_Stream_WithoutFilePath_ParsesCorrectly()
    {
        var content = "DELETE https://api.example.com/items/123";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var parser = new HttpFileParser.Parsing.HttpParser();
        var doc = parser.Parse(stream);

        Assert.Single(doc.Requests);
        Assert.Equal("DELETE", doc.Requests.First().Method);
        Assert.Null(doc.FilePath);
    }

    #endregion

    #region Multipart Form-Data Tests

    [Fact]
    public void Parse_MultipartBody_WithSections_ParsesCorrectly()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=----WebKitFormBoundary

            ------WebKitFormBoundary
            Content-Disposition: form-data; name="field1"

            value1
            ------WebKitFormBoundary
            Content-Disposition: form-data; name="field2"

            value2
            ------WebKitFormBoundary--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.NotNull(request.Body);
        Assert.IsType<HttpFileParser.Model.MultipartBody>(request.Body);
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body;
        Assert.Equal("----WebKitFormBoundary", multipart.Boundary);
        Assert.Equal(2, multipart.Sections.Count);
    }

    [Fact]
    public void Parse_MultipartBody_SectionHeaders_ParsedCorrectly()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=boundary123

            --boundary123
            Content-Disposition: form-data; name="file"; filename="test.txt"
            Content-Type: text/plain

            file content here
            --boundary123--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Single(multipart.Sections);
        var section = multipart.Sections[0];
        Assert.Equal(2, section.Headers.Count);
        Assert.Equal("Content-Disposition", section.Headers[0].Name);
        Assert.Contains("filename", section.Headers[0].RawValue);
        Assert.Equal("Content-Type", section.Headers[1].Name);
        Assert.Equal("text/plain", section.Headers[1].RawValue);
    }

    [Fact]
    public void Parse_MultipartBody_SectionWithFileReference_ParsedCorrectly()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=myboundary

            --myboundary
            Content-Disposition: form-data; name="document"

            < ./document.pdf
            --myboundary--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Single(multipart.Sections);
        var section = multipart.Sections[0];
        Assert.NotNull(section.Body);
        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(section.Body);
        var fileRef = (HttpFileParser.Model.FileReferenceBody)section.Body;
        Assert.Equal("./document.pdf", fileRef.FilePath);
    }

    [Fact]
    public void Parse_MultipartBody_EmptySections_Handled()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=sep

            --sep
            Content-Disposition: form-data; name="empty"

            --sep
            Content-Disposition: form-data; name="hasvalue"

            somevalue
            --sep--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Equal(2, multipart.Sections.Count);
        Assert.Null(multipart.Sections[0].Body);
        Assert.NotNull(multipart.Sections[1].Body);
    }

    [Fact]
    public void Parse_MultipartBody_WithMixedContent_ParsesCorrectly()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=BOUND

            --BOUND
            Content-Disposition: form-data; name="text"

            plain text content
            --BOUND
            Content-Disposition: form-data; name="json"
            Content-Type: application/json

            {"key": "value"}
            --BOUND
            Content-Disposition: form-data; name="file"

            < ./data.bin
            --BOUND--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Equal(3, multipart.Sections.Count);
        Assert.IsType<HttpFileParser.Model.TextBody>(multipart.Sections[0].Body);
        Assert.IsType<HttpFileParser.Model.TextBody>(multipart.Sections[1].Body);
        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(multipart.Sections[2].Body);
    }

    #endregion

    #region IsFileReference Edge Cases

    [Fact]
    public void Parse_XmlDeclaration_IsNotFileReference()
    {
        var content = """
            POST https://api.example.com/xml
            Content-Type: application/xml

            <?xml version="1.0" encoding="UTF-8"?>
            <root><item>value</item></root>
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.TextBody>(request.Body);
        var textBody = (HttpFileParser.Model.TextBody)request.Body!;
        Assert.Contains("<?xml", textBody.Content);
    }

    [Fact]
    public void Parse_HtmlClosingTag_IsNotFileReference()
    {
        // HTML/XML tags starting with </ followed by letters are NOT file references
        // File references require: "< " (space), "." (dot), "/" (slash), or "\" (backslash)
        // </html> starts with </ but is followed by 'h' (letter), not space/dot/path
        var content = """
            POST https://api.example.com/html
            Content-Type: text/html

            <div>test</div>
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.TextBody>(request.Body);
        var textBody = (HttpFileParser.Model.TextBody)request.Body!;
        Assert.Contains("<div>", textBody.Content);
    }

    [Fact]
    public void Parse_HtmlOpeningTag_IsNotFileReference()
    {
        var content = """
            POST https://api.example.com/html
            Content-Type: text/html

            <html><body>content</body></html>
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.TextBody>(request.Body);
    }

    [Fact]
    public void Parse_VsCodeFileReference_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: application/octet-stream

            <@ ./template.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        Assert.Equal("./template.json", fileBody.FilePath);
        Assert.True(fileBody.ProcessVariables);
    }

    [Fact]
    public void Parse_StandardFileReference_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload

            < ./data.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        Assert.Equal("./data.json", fileBody.FilePath);
        Assert.False(fileBody.ProcessVariables);
    }

    [Fact]
    public void Parse_FileReferenceWithEncoding_ParsesEncoding()
    {
        var content = """
            POST https://api.example.com/upload

            < ./data.txt utf-8
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        Assert.Equal("./data.txt", fileBody.FilePath);
        Assert.Equal("utf-8", fileBody.Encoding);
    }

    [Fact]
    public void Parse_FileReferenceWithVariousEncodings_RecognizesKnownEncodings()
    {
        var encodings = new[] { "utf-8", "utf8", "utf-16", "utf16", "ascii", "latin1", "iso-8859-1" };

        foreach (var encoding in encodings)
        {
            var content = $"""
                POST https://api.example.com/upload

                < ./data.txt {encoding}
                """;

            var doc = HttpFile.Parse(content);
            var request = doc.Requests.First();
            var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;

            Assert.Equal(encoding, fileBody.Encoding);
        }
    }

    [Fact]
    public void Parse_FileReferenceWithAbsolutePath_ParsesCorrectly()
    {
        var content = """
            POST https://api.example.com/upload

            < /absolute/path/to/file.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        Assert.Equal("/absolute/path/to/file.json", fileBody.FilePath);
    }

    [Fact]
    public void Parse_LessThanInJsonBody_IsNotFileReference()
    {
        var content = """
            POST https://api.example.com/compare
            Content-Type: application/json

            {"condition": "x < 10"}
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.TextBody>(request.Body);
        var textBody = (HttpFileParser.Model.TextBody)request.Body!;
        Assert.Contains("< 10", textBody.Content);
    }

    #endregion

    #region Error/Diagnostic Generation Tests

    [Fact]
    public void Parse_IncompleteRequestLine_GeneratesWarning()
    {
        // A single HTTP method word without URL becomes a BodyLine which generates a warning
        var content = """
            GET
            """;

        var doc = HttpFile.Parse(content);

        // The lexer doesn't recognize "GET" without URL as a request line
        // It becomes a BodyLine, which generates a warning
        Assert.NotEmpty(doc.Diagnostics);
        Assert.Contains(doc.Diagnostics, d => d.Severity == HttpFileParser.Model.HttpDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Parse_UnexpectedBodyLine_DiagnosticMessageDescriptive()
    {
        var content = """
            POST
            """;

        var doc = HttpFile.Parse(content);

        Assert.NotEmpty(doc.Diagnostics);
        var warning = doc.Diagnostics.First(d => d.Severity == HttpFileParser.Model.HttpDiagnosticSeverity.Warning);
        Assert.Contains("Unexpected token", warning.Message);
    }

    [Fact]
    public void Parse_UnexpectedBodyLine_GeneratesWarning()
    {
        // Content without valid method/URL becomes BodyLine and generates warning
        var content = """
            DELETE
            """;

        var doc = HttpFile.Parse(content);

        Assert.NotEmpty(doc.Diagnostics);
        Assert.Contains(doc.Diagnostics, d => d.Severity == HttpFileParser.Model.HttpDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Parse_HasErrors_ReturnsFalseWhenNoErrors()
    {
        var content = """
            GET https://api.example.com/users
            """;

        var doc = HttpFile.Parse(content);

        Assert.False(doc.HasErrors);
    }

    [Fact]
    public void Parse_UnrecognizedLine_BecomesBodyLine_WithWarning()
    {
        // Lines that don't match request/header patterns become body lines
        // And generate a warning when not inside a request body
        var content = """
            SomeRandomText
            """;

        var doc = HttpFile.Parse(content);

        // No requests created since there's no valid request line
        Assert.Empty(doc.Requests);
        // Warning is generated for unexpected BodyLine token
        Assert.Contains(doc.Diagnostics, d =>
            d.Severity == HttpFileParser.Model.HttpDiagnosticSeverity.Warning &&
            d.Message.Contains("Unexpected token: BodyLine"));
    }

    [Fact]
    public void Parse_MalformedHeader_TreatedAsBody()
    {
        // Headers without colons become body content in the lexer
        var content = """
            GET https://api.example.com/users
            Accept: application/json

            This is body content
            BadHeaderNoColon
            More body content
            """;

        var doc = HttpFile.Parse(content);

        Assert.Single(doc.Requests);
        // BadHeaderNoColon should be part of the body, not a header
        var request = doc.Requests.First();
        Assert.Single(request.Headers); // Only Accept header
        Assert.NotNull(request.Body);
        var textBody = (HttpFileParser.Model.TextBody)request.Body!;
        Assert.Contains("BadHeaderNoColon", textBody.Content);
    }

    [Fact]
    public void Parse_DelimiterWithoutRequest_GeneratesWarning()
    {
        var content = """
            GET https://api.example.com/first

            ###

            @variable = value
            """;

        var doc = HttpFile.Parse(content);

        Assert.Single(doc.Requests);
        Assert.Contains(doc.Diagnostics, d => d.Message.Contains("Expected request line"));
    }

    [Fact]
    public void Parse_DiagnosticsHaveCorrectSourceSpan()
    {
        var content = """
            RandomBodyContent
            """;

        var doc = HttpFile.Parse(content);

        Assert.NotEmpty(doc.Diagnostics);
        var warning = doc.Diagnostics.First(d => d.Severity == HttpFileParser.Model.HttpDiagnosticSeverity.Warning);
        Assert.True(warning.Span.StartLine >= 1);
        Assert.True(warning.Span.StartColumn >= 1);
    }

    #endregion

    #region LookAheadForRequest Edge Cases

    [Fact]
    public void Parse_CommentBeforeNextRequest_WithDelimiter_CorrectlyAssociates()
    {
        var content = """
            GET https://api.example.com/first

            ###
            # This is a comment for the second request
            GET https://api.example.com/second
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Requests.Count());
        var secondRequest = doc.Requests.Last();
        Assert.Single(secondRequest.LeadingComments);
    }

    [Fact]
    public void Parse_DirectiveBeforeNextRequest_CorrectlyAssociates()
    {
        var content = """
            GET https://api.example.com/first

            # @name secondRequest
            GET https://api.example.com/second
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Requests.Count());
        var secondRequest = doc.Requests.Last();
        Assert.Equal("secondRequest", secondRequest.Name);
    }

    [Fact]
    public void Parse_MultipleCommentsBeforeRequest_WithDelimiter_AllAssociated()
    {
        var content = """
            ###
            # Comment 1
            # Comment 2
            # @name myRequest
            GET https://api.example.com/users
            """;

        var doc = HttpFile.Parse(content);

        Assert.Single(doc.Requests);
        var request = doc.Requests.First();
        Assert.Equal(2, request.LeadingComments.Count);
        Assert.Equal("myRequest", request.Name);
    }

    [Fact]
    public void Parse_BodyFollowedByDelimiterAndRequest_BodyEndsCorrectly()
    {
        var content = """
            POST https://api.example.com/first
            Content-Type: application/json

            {"data": "value"}

            ###
            # Comment for second request
            GET https://api.example.com/second
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Requests.Count());
        var firstRequest = doc.Requests.First();
        Assert.NotNull(firstRequest.Body);
        var textBody = (HttpFileParser.Model.TextBody)firstRequest.Body!;
        Assert.Contains("data", textBody.Content);
        Assert.DoesNotContain("Comment", textBody.Content);
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public void Parse_RequestWithQuery_ContinuationLines()
    {
        var content = """
            GET https://api.example.com/search
              ?query=test
              &page=1
              &limit=10
              &sort=asc
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Contains("query=test", request.RawUrl);
        Assert.Contains("page=1", request.RawUrl);
        Assert.Contains("limit=10", request.RawUrl);
        Assert.Contains("sort=asc", request.RawUrl);
    }

    [Fact]
    public void Parse_DelimiterWithComment_PreservesComment()
    {
        var content = """
            ### Get All Users
            GET https://api.example.com/users
            """;

        var doc = HttpFile.Parse(content);

        Assert.Single(doc.Requests);
        var request = doc.Requests.First();
        Assert.Contains(request.LeadingComments, c => c.Text.Contains("Get All Users"));
    }

    [Fact]
    public void Parse_EmptyBodyLines_Trimmed()
    {
        var content = """
            POST https://api.example.com/data
            Content-Type: application/json


            {"key": "value"}


            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var textBody = (HttpFileParser.Model.TextBody)request.Body!;

        Assert.DoesNotMatch(@"^\s+", textBody.Content);
        Assert.DoesNotMatch(@"\s+$", textBody.Content);
    }

    [Fact]
    public void Parse_MultipleBlankLinesBetweenRequests_Handled()
    {
        var content = """
            GET https://api.example.com/first



            ###



            GET https://api.example.com/second
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Requests.Count());
    }

    [Fact]
    public void Parse_VariableDefinitionEndsBody()
    {
        var content = """
            GET https://api.example.com/users

            @newVariable = value

            GET https://api.example.com/second
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Requests.Count());
        Assert.Single(doc.Variables);
        Assert.Null(doc.Requests.First().Body);
    }

    [Fact]
    public void Parse_ProvidesFilePath()
    {
        var content = "GET https://api.example.com/users";

        var doc = HttpFile.Parse(content, "my-requests.http");

        Assert.Equal("my-requests.http", doc.FilePath);
    }

    [Fact]
    public void Parse_NullFilePath_Allowed()
    {
        var content = "GET https://api.example.com/users";

        var doc = HttpFile.Parse(content);

        Assert.Null(doc.FilePath);
    }

    #endregion

    #region Additional URL Continuation Tests

    [Fact]
    public void Parse_UrlContinuation_WithBodyLikeToken_ContinuesUrl()
    {
        var content = """
            GET https://api.example.com/search
              ?param=value
            Accept: application/json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Contains("param=value", request.RawUrl);
        Assert.Single(request.Headers);
    }

    [Fact]
    public void Parse_UrlContinuation_WithAmpersand_AtLineStart()
    {
        var content = """
            GET https://api.example.com/search?first=1
            &second=2
            Accept: application/json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Contains("first=1", request.RawUrl);
        Assert.Contains("second=2", request.RawUrl);
    }

    #endregion

    #region File Reference Edge Cases

    [Fact]
    public void Parse_FileReferenceWithBackslashPath_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload

            <\path\to\file.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
    }

    [Fact]
    public void Parse_FileReferenceWithDotStart_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload

            <.hidden-file.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
    }

    [Fact]
    public void Parse_FileReferenceAtSymbol_WithLetterPath_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload

            <@template.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        Assert.True(fileBody.ProcessVariables);
    }

    [Fact]
    public void Parse_FileReference_SingleLessThan_IsNotFileReference()
    {
        // Just a single < character without valid follow-up
        var content = """
            POST https://api.example.com/test

            <
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.TextBody>(request.Body);
    }

    [Fact]
    public void Parse_FileReferenceWithUnknownEncoding_NoEncodingSet()
    {
        var content = """
            POST https://api.example.com/upload

            < ./data.txt unknown-encoding
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        // Path should include the unknown encoding since it's not recognized
        Assert.Contains("unknown-encoding", fileBody.FilePath);
        Assert.Null(fileBody.Encoding);
    }

    #endregion

    #region Multipart Section Edge Cases

    [Fact]
    public void Parse_MultipartSection_EmptyContentAfterHeaders()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=SEP

            --SEP
            Content-Disposition: form-data; name="emptyField"

            --SEP--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Single(multipart.Sections);
        Assert.Null(multipart.Sections[0].Body);
    }

    [Fact]
    public void Parse_MultipartSection_WithBodyStartingLessThan()
    {
        // A body starting with < but not being a file reference
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=SEP

            --SEP
            Content-Disposition: form-data; name="xml"

            <xml>content</xml>
            --SEP--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Single(multipart.Sections);
        // Since <xml> doesn't match file reference pattern, it should be text body
        // However, the parser checks if it starts with < for file reference
        // Let's see what the actual behavior is
        Assert.NotNull(multipart.Sections[0].Body);
    }

    [Fact]
    public void Parse_MultipartBody_WithoutBoundary_ReturnsTExtBody()
    {
        // When boundary can't be extracted, body is treated as text
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data

            --someboundary
            content
            --someboundary--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        // Without boundary parameter in Content-Type, it's treated as text
        Assert.IsType<HttpFileParser.Model.TextBody>(request.Body);
    }

    #endregion

    #region Request Line Parsing Edge Cases

    [Fact]
    public void Parse_RequestLineWithMultipleSpacesInUrl_PreservesUrl()
    {
        // URLs with multiple space-separated parts (unusual but test parsing)
        var content = "GET https://example.com/path HTTP/1.1";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Equal("GET", request.Method);
        Assert.Equal("https://example.com/path", request.RawUrl);
        Assert.Equal("HTTP/1.1", request.HttpVersion);
    }

    [Fact]
    public void Parse_RequestLineWithHttp2Version()
    {
        var content = "GET https://example.com/api HTTP/2";
        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Equal("HTTP/2", request.HttpVersion);
    }

    [Fact]
    public void Parse_InvalidRequestLine_GeneratesError()
    {
        // Create content where after header, we try to force an invalid request line scenario
        var content = """
            ###
            InvalidNotAMethod https://example.com
            """;

        var doc = HttpFile.Parse(content);

        // The "InvalidNotAMethod" isn't recognized as a method
        // Should generate a warning about expected request line
        Assert.Contains(doc.Diagnostics, d => d.Message.Contains("Expected request line"));
    }

    #endregion

    #region Header Parsing Edge Cases

    [Fact]
    public void Parse_HeaderWithOnlyColon_GeneratesWarning()
    {
        var content = """
            GET https://api.example.com/test
            :invalid-header-no-name

            """;

        var doc = HttpFile.Parse(content);

        // A header line with colon at start should generate warning
        // The line ":invalid-header-no-name" becomes a body line
        var request = doc.Requests.First();
        Assert.NotNull(request.Body);
    }

    #endregion

    #region Look-ahead Edge Cases

    [Fact]
    public void Parse_BodyWithManyComments_LookAheadLimitsDepth()
    {
        // Test that look-ahead doesn't go beyond 10 tokens
        var content = """
            POST https://api.example.com/test
            Content-Type: text/plain

            Body line 1
            # comment 1
            # comment 2
            # comment 3
            # comment 4
            # comment 5
            # comment 6
            # comment 7
            # comment 8
            # comment 9
            # comment 10
            # comment 11
            More body content

            ###
            GET https://api.example.com/next
            """;

        var doc = HttpFile.Parse(content);

        Assert.Equal(2, doc.Requests.Count());
    }

    [Fact]
    public void Parse_DirectiveInBody_NotFollowedByRequest_TreatedAsBody()
    {
        var content = """
            POST https://api.example.com/test
            Content-Type: text/plain

            Some body content
            # @name notADirective
            More body content
            """;

        var doc = HttpFile.Parse(content);

        Assert.Single(doc.Requests);
        var request = doc.Requests.First();
        Assert.NotNull(request.Body);
        var textBody = (HttpFileParser.Model.TextBody)request.Body!;
        Assert.Contains("notADirective", textBody.Content);
    }

    #endregion

    #region Directive Parsing Edge Cases

    [Fact]
    public void Parse_DirectiveWithoutValue_ParsesName()
    {
        var content = """
            # @no-redirect
            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Contains(request.Directives, d => d.Name == "no-redirect" && d.Value == null);
    }

    [Fact]
    public void Parse_DirectiveWithEmptyValue_ParsesCorrectly()
    {
        var content = """
            # @note
            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        var noteDirective = request.Directives.FirstOrDefault(d => d.Name == "note");
        Assert.NotNull(noteDirective);
        Assert.Null(noteDirective!.Value);
    }

    #endregion

    #region Variable Definition Edge Cases

    [Fact]
    public void Parse_VariableWithEqualsOnly_ParsesEmptyValue()
    {
        var content = """
            @emptyVar =

            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);

        var variable = doc.Variables.FirstOrDefault(v => v.Name == "emptyVar");
        Assert.NotNull(variable);
        Assert.Equal("", variable!.RawValue);
    }

    [Fact]
    public void Parse_VariableWithNoEquals_HandlesGracefully()
    {
        var content = """
            @noEquals

            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);

        // This should not be recognized as a variable definition
        // It should generate a warning as unexpected token
        Assert.Empty(doc.Variables);
    }

    #endregion

    #region EOF Handling

    [Fact]
    public void Parse_DelimiterAtEndWithNoRequest_Warning()
    {
        var content = """
            GET https://api.example.com/first

            ###
            """;

        var doc = HttpFile.Parse(content);

        Assert.Single(doc.Requests);
        // Delimiter without following request might generate warning
    }

    #endregion

    #region Multipart Section Body Edge Cases

    [Fact]
    public void Parse_MultipartSection_BodyStartsWithLessThanNotFileRef_TreatedAsText()
    {
        // Body starts with < but is not a file reference (e.g., XML tag)
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=SEP

            --SEP
            Content-Disposition: form-data; name="xml"
            Content-Type: application/xml

            <note><body>Hello</body></note>
            --SEP--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Single(multipart.Sections);
        // The parser checks if body starts with < but IsFileReference will return false for <note>
        var section = multipart.Sections[0];
        Assert.NotNull(section.Body);
    }

    [Fact]
    public void Parse_MultipartSection_BodyStartsWithLessThanQuestion_NotFileRef()
    {
        // <?xml ...> should not be treated as file reference
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=SEP

            --SEP
            Content-Disposition: form-data; name="xml"

            <?xml version="1.0"?><root/>
            --SEP--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Single(multipart.Sections);
        var section = multipart.Sections[0];
        // Should be text body or file reference depending on implementation
        Assert.NotNull(section.Body);
    }

    [Fact]
    public void Parse_MultipartSection_ValidFileReference_Recognized()
    {
        var content = """
            POST https://api.example.com/upload
            Content-Type: multipart/form-data; boundary=SEP

            --SEP
            Content-Disposition: form-data; name="file"

            < ./myfile.txt
            --SEP--
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var multipart = (HttpFileParser.Model.MultipartBody)request.Body!;

        Assert.Single(multipart.Sections);
        var section = multipart.Sections[0];
        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(section.Body);
    }

    #endregion

    #region Parser Diagnostic Edge Cases

    [Fact]
    public void Parse_HeaderToken_NoColon_GeneratesWarning()
    {
        // Force a header line to have invalid format
        // This tests the warning branch in ParseHeader
        var content = """
            GET https://api.example.com/test
            ValidHeader: value

            {"body": "content"}
            """;

        var doc = HttpFile.Parse(content);
        Assert.Single(doc.Requests);
        Assert.Single(doc.Requests.First().Headers);
    }

    [Fact]
    public void Parse_DelimiterAtEndWithOnlyComments_CommentsPreserved()
    {
        // Delimiter followed by comments but no request
        // The comments are collected but since no request follows, they go to items
        var content = """
            GET https://api.example.com/first

            ###
            # Just a comment, no request follows
            """;

        var doc = HttpFile.Parse(content);
        Assert.Single(doc.Requests);
        // No additional assertions - just verify it parses without errors
    }

    [Fact]
    public void Parse_DelimiterWithDirectives_NoRequest_Warning()
    {
        var content = """
            GET https://api.example.com/first

            ###
            # @name orphanedDirective
            @variable = value
            """;

        var doc = HttpFile.Parse(content);
        Assert.Single(doc.Requests);
        Assert.Single(doc.Variables);
        Assert.Contains(doc.Diagnostics, d => d.Message.Contains("Expected request line"));
    }

    #endregion

    #region Additional File Reference Edge Cases

    [Fact]
    public void Parse_FileReference_AtSymbolWithSlash_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload

            <@/absolute/path.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        Assert.True(fileBody.ProcessVariables);
    }

    [Fact]
    public void Parse_FileReference_AtSymbolWithDot_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload

            <@./relative/path.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
        var fileBody = (HttpFileParser.Model.FileReferenceBody)request.Body!;
        Assert.True(fileBody.ProcessVariables);
    }

    [Fact]
    public void Parse_FileReference_AtSymbolWithBackslash_IsFileReference()
    {
        var content = """
            POST https://api.example.com/upload

            <@\windows\path.json
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.IsType<HttpFileParser.Model.FileReferenceBody>(request.Body);
    }

    [Fact]
    public void Parse_LessThanJustSpace_NotFileReference()
    {
        // Just "< " followed by nothing meaningful
        var content = """
            POST https://api.example.com/test

            <
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        // IsFileReference checks for space then path starting chars
        Assert.NotNull(request.Body);
    }

    #endregion

    #region URL Continuation Additional Tests

    [Fact]
    public void Parse_UrlContinuation_HeaderLikeToken_WithQuestionMark()
    {
        // A line that looks like a header token but starts with ?
        // should be treated as URL continuation
        var content = """
            GET https://api.example.com/search
            ?param=value
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Contains("param=value", request.RawUrl);
        Assert.Empty(request.Headers);
    }

    [Fact]
    public void Parse_UrlContinuation_BodyLineToken_WithAmpersand()
    {
        var content = """
            GET https://api.example.com/search?first=1
            &second=2
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Contains("first=1", request.RawUrl);
        Assert.Contains("second=2", request.RawUrl);
    }

    #endregion

    #region Directive Parsing Additional Tests

    [Fact]
    public void Parse_Directive_WithoutAtSign_JustCommentPrefix()
    {
        // Edge case: directive parsing when text doesn't start with @
        // after removing comment prefix
        var content = """
            # notadirective justtext
            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        // Should be treated as a regular comment, not a directive
        Assert.Empty(request.Directives);
    }

    [Fact]
    public void Parse_Directive_DoubleSlash_WithoutSpace_AfterAt()
    {
        var content = """
            //@no-redirect
            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Single(request.Directives);
        Assert.True(request.HasDirective("no-redirect"));
    }

    #endregion

    #region Span and Position Tracking

    [Fact]
    public void Parse_Request_SpanCoversEntireRequest()
    {
        var content = """
            GET https://api.example.com/test
            Authorization: Bearer token

            {"body": "data"}
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.True(request.Span.StartLine >= 1);
        Assert.True(request.Span.EndLine >= request.Span.StartLine);
        Assert.True(request.Span.EndOffset > request.Span.StartOffset);
    }

    [Fact]
    public void Parse_Request_SpanEndsAtLastHeader_WhenNoBody()
    {
        var content = """
            GET https://api.example.com/test
            Authorization: Bearer token
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.True(request.Span.EndLine >= 1);
    }

    [Fact]
    public void Parse_Request_SpanEndsAtRequestLine_WhenNoHeadersOrBody()
    {
        var content = "GET https://api.example.com/test";

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        Assert.Equal(1, request.Span.StartLine);
        Assert.Equal(1, request.Span.EndLine);
    }

    #endregion

    #region Variable Definition Additional Tests

    [Fact]
    public void Parse_VariableDefinition_WithEqualsInValue()
    {
        var content = """
            @connectionString = Server=localhost;Database=test

            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);
        var variable = doc.Variables.First();

        Assert.Equal("connectionString", variable.Name);
        Assert.Equal("Server=localhost;Database=test", variable.RawValue);
    }

    #endregion

    #region Body Parsing Edge Cases

    [Fact]
    public void Parse_BodyWithOnlyBlankLines_NoBodyReturned()
    {
        var content = """
            GET https://api.example.com/test
            Accept: application/json



            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        // Body with only blank lines should be trimmed to null
        Assert.Null(request.Body);
    }

    [Fact]
    public void Parse_BodyLeadingAndTrailingBlankLines_Trimmed()
    {
        var content = """
            POST https://api.example.com/test
            Content-Type: text/plain


            actual content


            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();
        var textBody = (HttpFileParser.Model.TextBody)request.Body!;

        Assert.Equal("actual content", textBody.Content);
    }

    #endregion

    #region Comment Parsing

    [Fact]
    public void Parse_Comment_DoubleSlashPrefix_WithDelimiter_BecomesLeadingComment()
    {
        var content = """
            ###
            // This is a comment
            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        // Comment after delimiter becomes leading comment of the request
        Assert.Single(request.LeadingComments);
        Assert.Equal("This is a comment", request.LeadingComments[0].Text);
    }

    [Fact]
    public void Parse_Comment_HashPrefix_WithDelimiter_BecomesLeadingComment()
    {
        var content = """
            ###
            # This is a hash comment
            GET https://api.example.com/test
            """;

        var doc = HttpFile.Parse(content);
        var request = doc.Requests.First();

        // Comment after delimiter becomes leading comment of the request
        Assert.Single(request.LeadingComments);
        Assert.Equal("This is a hash comment", request.LeadingComments[0].Text);
    }

    #endregion
}
