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

    #region Line Ending Edge Cases

    [Fact]
    public void Tokenize_ContentWithCarriageReturnOnly_HandlesCorrectly()
    {
        // Test standalone \r line endings (old Mac style)
        var content = "GET /test\rHeader: value";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.Header, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_ContentWithCRLF_HandlesCorrectly()
    {
        var content = "GET /test\r\nHeader: value\r\n";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.Header, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_BlankLineWithCarriageReturnOnly_ReturnsBlankLine()
    {
        var content = "GET /test\r\rBody content";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.BlankLine, tokens[1].Type);
        Assert.Equal(TokenType.BodyLine, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_WhitespaceOnlyLine_ReturnsBlankLine()
    {
        var content = "GET /test\n   \nBody";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.BlankLine, tokens[1].Type);
        Assert.Equal(TokenType.BodyLine, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_TrailingWhitespaceAtEndOfFile_ReturnsBlankLine()
    {
        var content = "GET /test\n   ";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.BlankLine, tokens[1].Type);
        Assert.Equal(TokenType.EOF, tokens[2].Type);
    }

    #endregion

    #region Variable Definition Edge Cases

    [Fact]
    public void Tokenize_VariableWithHyphenInName_RecognizedAsVariable()
    {
        var content = "@base-url = https://api.example.com";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.VariableDefinition, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_VariableWithUnderscore_RecognizedAsVariable()
    {
        var content = "@base_url = https://api.example.com";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.VariableDefinition, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_AtSignWithoutEquals_NotVariableDefinition()
    {
        // @ followed by name but no = sign should not be a variable definition
        var content = "@notAvariable something";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        // It becomes a body line since it's not a valid variable definition
        Assert.Equal(TokenType.BodyLine, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_AtSignAlone_NotVariableDefinition()
    {
        var content = "@ = value";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        // @ without a name is not a variable definition
        Assert.Equal(TokenType.BodyLine, tokens[0].Type);
    }

    #endregion

    #region Directive Edge Cases

    [Fact]
    public void Tokenize_DirectiveWithDoubleSlash_ReturnsDirectiveToken()
    {
        var content = "// @name myRequest";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Directive, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_CommentWithHashNotDirective_ReturnsCommentToken()
    {
        var content = "# This is just a comment";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_DoubleSlashCommentWithoutDirective_ReturnsCommentToken()
    {
        var content = "// Just a simple comment";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
    }

    #endregion

    #region Request Line Edge Cases

    [Fact]
    public void Tokenize_ConnectMethod_ReturnsRequestLine()
    {
        var content = "CONNECT proxy.example.com:443";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_TraceMethod_ReturnsRequestLine()
    {
        var content = "TRACE https://example.com/debug";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_MethodWithoutSpace_ReturnsBodyLine()
    {
        // A method without URL should not be recognized as request line
        var content = "GET";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.BodyLine, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_LowercaseMethod_ReturnsRequestLine()
    {
        var content = "get https://api.example.com/test";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
    }

    #endregion

    #region Header Edge Cases

    [Fact]
    public void Tokenize_HeaderWithColonInValue_ReturnsHeaderToken()
    {
        var content = "Authorization: Basic dXNlcjpwYXNz";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Header, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_HeaderWithEmptyValue_ReturnsHeaderToken()
    {
        var content = "X-Empty:";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Header, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_HeaderNameWithSpaces_ReturnsBodyLine()
    {
        // Header name with space before colon should be treated as body
        var content = "Invalid Header: value";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.BodyLine, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_ColonAtStart_ReturnsBodyLine()
    {
        // Line starting with colon is not a header
        var content = ": not a header";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.BodyLine, tokens[0].Type);
    }

    #endregion

    #region Position Tracking Tests

    [Fact]
    public void Tokenize_TracksLineNumbers()
    {
        var content = "GET /test\nHeader: value\nBody";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(2, tokens[1].Line);
        Assert.Equal(3, tokens[2].Line);
    }

    [Fact]
    public void Tokenize_TracksColumnNumbers()
    {
        var content = "GET /test";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(1, tokens[0].Column);
    }

    [Fact]
    public void Tokenize_TracksOffsets()
    {
        var content = "GET /test\nHeader: value";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(0, tokens[0].StartOffset);
        // First token ends after "GET /test\n" = 10 chars
        Assert.Equal(10, tokens[1].StartOffset);
    }

    #endregion

    #region IndentedContent Tests

    [Fact]
    public void Tokenize_IndentedRequestDelimiter_RecognizedWithWhitespace()
    {
        var content = "  ###";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestDelimiter, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_IndentedComment_RecognizedWithWhitespace()
    {
        var content = "  # comment";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
    }

    #endregion

    #region EOF and End-of-Content Edge Cases

    [Fact]
    public void Tokenize_OnlyWhitespaceAtEnd_HandlesEndOfContent()
    {
        // Tests the edge case where IsBlankLine returns true when
        // we reach end of content with only whitespace (i > _position branch)
        var content = "GET /test\n   ";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.BlankLine, tokens[1].Type);
        Assert.Equal(TokenType.EOF, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_EmptyLineBeforeEOF_ProperlyTerminates()
    {
        // Ensure the EOF token is properly generated after processing
        var content = "GET /test\n";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.EOF, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_MultipleCallsToTokenize_ReturnsConsistentResults()
    {
        var content = "GET /test";
        var lexer = new HttpLexer(content);

        var tokens1 = lexer.Tokenize().ToList();

        Assert.Equal(2, tokens1.Count);
        Assert.Equal(TokenType.RequestLine, tokens1[0].Type);
        Assert.Equal(TokenType.EOF, tokens1[1].Type);
    }

    [Fact]
    public void Tokenize_ContentEndsWithWhitespaceOnly_ReturnsBlankLine()
    {
        // This specifically tests the edge case in IsBlankLine where
        // we reach end of content with only whitespace processed
        var content = "GET /test\n\t  ";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.BlankLine, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_TabsOnlyLine_ReturnsBlankLine()
    {
        var content = "GET /test\n\t\t\t\nBody";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.BlankLine, tokens[1].Type);
        Assert.Equal(TokenType.BodyLine, tokens[2].Type);
    }

    #endregion

    #region MatchesAhead Edge Cases

    [Fact]
    public void Tokenize_DelimiterAtEndOfContent_Recognized()
    {
        var content = "###";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestDelimiter, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_PartialDelimiter_NotRecognized()
    {
        // ## is not a delimiter (needs ###)
        var content = "##";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
    }

    #endregion

    #region Mixed Line Endings

    [Fact]
    public void Tokenize_MixedLineEndings_HandlesAll()
    {
        // Mix of \n, \r\n, and \r
        var content = "GET /test1\nGET /test2\r\nGET /test3\rGET /test4";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        var requestTokens = tokens.Where(t => t.Type == TokenType.RequestLine).ToList();
        Assert.Equal(4, requestTokens.Count);
    }

    [Fact]
    public void Tokenize_CarriageReturnAtEndOfFile_HandlesCorrectly()
    {
        var content = "GET /test\r";
        var lexer = new HttpLexer(content);
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.RequestLine, tokens[0].Type);
        Assert.Equal(TokenType.EOF, tokens[1].Type);
    }

    #endregion
}
