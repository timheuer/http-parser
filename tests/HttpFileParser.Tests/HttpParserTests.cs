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
}
