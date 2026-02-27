using System.Text;

namespace HttpFileParser.Parsing;

/// <summary>
/// Lexer for HTTP file format. Tokenizes input into meaningful tokens.
/// </summary>
public sealed class HttpLexer
{
    private readonly string _content;
    private int _position;
    private int _line;
    private int _column;

    private static readonly HashSet<string> HttpMethods =
    [
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE", "CONNECT"
    ];

    public HttpLexer(string content)
    {
        _content = content;
        _position = 0;
        _line = 1;
        _column = 1;
    }

    public IEnumerable<Token> Tokenize()
    {
        while (!IsAtEnd)
        {
            var token = ReadNextToken();
            yield return token;
            if (token.Type == TokenType.EOF)
            {
                yield break;
            }
        }

        yield return new Token(TokenType.EOF, "", _line, _column, _position, _position);
    }

    private Token ReadNextToken()
    {
        if (IsAtEnd)
        {
            return new Token(TokenType.EOF, "", _line, _column, _position, _position);
        }

        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;

        // Check for blank line (only whitespace before newline)
        if (IsBlankLine())
        {
            var blankText = ReadLine();
            return new Token(TokenType.BlankLine, blankText, startLine, startColumn, startPosition, _position);
        }

        // Skip leading whitespace on the line (indentation)
        SkipInlineWhitespace();

        // Check for request delimiter (###)
        if (MatchesAhead("###"))
        {
            var text = ReadLine();
            return new Token(TokenType.RequestDelimiter, text, startLine, startColumn, startPosition, _position);
        }

        // Check for comment (# or //)
        if (Peek() == '#' && !MatchesAhead("###"))
        {
            // Could be a comment or a directive (@name in comment)
            var lineContent = PeekLine();
            if (IsDirectiveLine(lineContent))
            {
                var text = ReadLine();
                return new Token(TokenType.Directive, text, startLine, startColumn, startPosition, _position);
            }
            var commentText = ReadLine();
            return new Token(TokenType.Comment, commentText, startLine, startColumn, startPosition, _position);
        }

        if (MatchesAhead("//"))
        {
            var lineContent = PeekLine();
            if (IsDirectiveLine(lineContent))
            {
                var text = ReadLine();
                return new Token(TokenType.Directive, text, startLine, startColumn, startPosition, _position);
            }
            var commentText = ReadLine();
            return new Token(TokenType.Comment, commentText, startLine, startColumn, startPosition, _position);
        }

        // Check for variable definition (@varName = value)
        if (Peek() == '@')
        {
            var lineContent = PeekLine();
            if (IsVariableDefinition(lineContent))
            {
                var text = ReadLine();
                return new Token(TokenType.VariableDefinition, text, startLine, startColumn, startPosition, _position);
            }
        }

        // Check for request line (METHOD URL [HTTP/version])
        if (IsRequestLine(PeekLine()))
        {
            var text = ReadLine();
            return new Token(TokenType.RequestLine, text, startLine, startColumn, startPosition, _position);
        }

        // Check for header (Name: Value)
        if (IsHeaderLine(PeekLine()))
        {
            var text = ReadLine();
            return new Token(TokenType.Header, text, startLine, startColumn, startPosition, _position);
        }

        // Everything else is body content
        var bodyText = ReadLine();
        return new Token(TokenType.BodyLine, bodyText, startLine, startColumn, startPosition, _position);
    }

    private bool IsBlankLine()
    {
        var i = _position;
        while (i < _content.Length)
        {
            var c = _content[i];
            if (c == '\n')
            {
                return true;
            }

            if (c == '\r')
            {
                // Check for \r\n or just \r
                if (i + 1 < _content.Length && _content[i + 1] == '\n')
                {
                    return true;
                }

                return true;
            }

            if (!char.IsWhiteSpace(c))
            {
                return false;
            }

            i++;
        }

        // End of content with only whitespace
        return i > _position;
    }

    private void SkipInlineWhitespace()
    {
        while (!IsAtEnd && (Peek() == ' ' || Peek() == '\t'))
        {
            Advance();
        }
    }

    private string ReadLine()
    {
        var start = _position;
        while (!IsAtEnd && Peek() != '\n' && Peek() != '\r')
        {
            Advance();
        }

        // Consume newline
        if (!IsAtEnd)
        {
            if (Peek() == '\r')
            {
                Advance();
                if (!IsAtEnd && Peek() == '\n')
                {
                    Advance();
                }
            }
            else if (Peek() == '\n')
            {
                Advance();
            }
        }

        return _content[start.._position];
    }

    private string PeekLine()
    {
        var sb = new StringBuilder();
        var i = _position;
        while (i < _content.Length && _content[i] != '\n' && _content[i] != '\r')
        {
            sb.Append(_content[i]);
            i++;
        }

        return sb.ToString();
    }

    private bool MatchesAhead(string text)
    {
        if (_position + text.Length > _content.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (_content[_position + i] != text[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDirectiveLine(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(1).TrimStart();
        }
        else if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(2).TrimStart();
        }
        else
        {
            return false;
        }

        return trimmed.StartsWith("@", StringComparison.Ordinal);
    }

    private static bool IsVariableDefinition(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            return false;
        }

        // Find the variable name and check for '='
        var i = 1;
        while (i < trimmed.Length && (char.IsLetterOrDigit(trimmed[i]) || trimmed[i] == '_' || trimmed[i] == '-'))
        {
            i++;
        }

        if (i == 1)
        {
            return false; // No variable name
        }

        // Skip whitespace
        while (i < trimmed.Length && (trimmed[i] == ' ' || trimmed[i] == '\t'))
        {
            i++;
        }

        return i < trimmed.Length && trimmed[i] == '=';
    }

    private static bool IsRequestLine(string line)
    {
        var trimmed = line.TrimStart();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            return false;
        }

        var method = trimmed[..spaceIndex].ToUpperInvariant();
        return HttpMethods.Contains(method);
    }

    private static bool IsHeaderLine(string line)
    {
        var trimmed = line.TrimStart();
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false;
        }

        // Header name must be valid (no spaces before colon)
        var headerName = trimmed[..colonIndex];
        return !string.IsNullOrWhiteSpace(headerName) && !headerName.Contains(' ');
    }

    private char Peek() => IsAtEnd ? '\0' : _content[_position];

    private char Advance()
    {
        if (IsAtEnd)
        {
            return '\0';
        }

        var c = _content[_position++];
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        return c;
    }

    private bool IsAtEnd => _position >= _content.Length;
}
