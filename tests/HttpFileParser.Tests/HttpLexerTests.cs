using HttpFileParser.Model;
using HttpFileParser.Parsing;

namespace HttpFileParser.Tests;

public class HttpLexerTests
{
    [Fact]
    public void Tokenize_EmptyContent_ReturnsEOF()
    {
        var lexer = new HttpLexer("");
        var tokens = lexer.Tokenize().ToList();

        Assert.Single(tokens);
        Assert.Equal(TokenType.EOF, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_SimpleRequest_ReturnsCorrectTokens()
    {
        var content = "GET https://api.example.com/users";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Contains("GET", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_RequestWithHeaders_ReturnsAllTokens()
    {
        var content = """
            GET https://api.example.com/users
            Authorization: Bearer token
            Content-Type: application/json
            """;

        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.Header, tokens[1].Type);
        Assert.Equal(TokenType.Header, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_Comment_ReturnsCommentToken()
    {
        var content = "# This is a comment";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_DoubleSlashComment_ReturnsCommentToken()
    {
        var content = "// This is a comment";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_RequestDelimiter_ReturnsDelimiterToken()
    {
        var content = "### My Request";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestDelimiter, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_VariableDefinition_ReturnsVariableToken()
    {
        var content = "@baseUrl = https://api.example.com";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.VariableDefinition, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_Directive_ReturnsDirectiveToken()
    {
        var content = "# @name myRequest";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Directive, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_BlankLine_ReturnsBlankLineToken()
    {
        var content = "GET /test\n\nBody content";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.BlankLine, tokens[1].Type);
        Assert.Equal(TokenType.BodyLine, tokens[2].Type);
    }
}
