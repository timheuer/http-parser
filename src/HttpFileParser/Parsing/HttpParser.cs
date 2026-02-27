using System.Text;
using System.Text.RegularExpressions;
using HttpFileParser.Model;

namespace HttpFileParser.Parsing;

/// <summary>
/// Parser for HTTP file format. Converts tokens into a document model.
/// </summary>
public sealed partial class HttpParser
{
    private readonly List<HttpDiagnostic> _diagnostics = [];
    private readonly List<Token> _tokens;
    private int _tokenIndex;
    private string? _filePath;

    public HttpParser()
    {
        _tokens = [];
    }

    public HttpDocument Parse(string content, string? filePath = null)
    {
        _filePath = filePath;
        _diagnostics.Clear();

        var lexer = new HttpLexer(content);
        _tokens.Clear();
        _tokens.AddRange(lexer.Tokenize());
        _tokenIndex = 0;

        var items = ParseDocumentItems();
        return new HttpDocument(filePath, items, _diagnostics.ToList());
    }

    public HttpDocument Parse(Stream stream, string? filePath = null)
    {
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        return Parse(content, filePath);
    }

    private List<HttpDocumentItem> ParseDocumentItems()
    {
        var items = new List<HttpDocumentItem>();
        var pendingComments = new List<Comment>();
        var pendingDirectives = new List<HttpDirective>();

        while (!IsAtEnd())
        {
            var token = Current();

            switch (token.Type)
            {
                case TokenType.BlankLine:
                    // Blank lines separate sections; clear pending comments if not followed by request
                    Advance();
                    break;

                case TokenType.Comment:
                    pendingComments.Add(ParseComment(token));
                    Advance();
                    break;

                case TokenType.VariableDefinition:
                    // Variable definitions at file level; clear pending comments
                    foreach (var comment in pendingComments)
                    {
                        items.Add(comment);
                    }
                    pendingComments.Clear();
                    pendingDirectives.Clear();
                    items.Add(ParseVariableDefinition(token));
                    Advance();
                    break;

                case TokenType.RequestDelimiter:
                    // Start of a new request section
                    var request = ParseRequest(pendingComments, pendingDirectives);
                    if (request != null)
                    {
                        items.Add(request);
                    }
                    pendingComments.Clear();
                    pendingDirectives.Clear();
                    break;

                case TokenType.RequestLine:
                    // Request without explicit delimiter
                    var requestWithoutDelimiter = ParseRequestFromLine(pendingComments, pendingDirectives);
                    if (requestWithoutDelimiter != null)
                    {
                        items.Add(requestWithoutDelimiter);
                    }
                    pendingComments.Clear();
                    pendingDirectives.Clear();
                    break;

                case TokenType.Directive:
                    // Collect directives that might belong to a following request
                    pendingDirectives.Add(ParseDirective(token));
                    Advance();
                    break;

                case TokenType.EOF:
                    // Add any remaining comments (directives without requests become lost)
                    items.AddRange(pendingComments);
                    return items;

                default:
                    // Skip unexpected tokens
                    AddDiagnostic(HttpDiagnosticSeverity.Warning, $"Unexpected token: {token.Type}", token);
                    Advance();
                    break;
            }
        }

        items.AddRange(pendingComments);
        return items;
    }

    private HttpRequest? ParseRequest(List<Comment> leadingComments, List<HttpDirective> pendingDirectives)
    {
        var startToken = Current();
        var directives = new List<HttpDirective>(pendingDirectives);
        Comment? delimiterComment = null;

        // Parse delimiter (###)
        if (Current().Type == TokenType.RequestDelimiter)
        {
            var delimiterText = ExtractDelimiterComment(Current().Text);
            if (!string.IsNullOrWhiteSpace(delimiterText))
            {
                delimiterComment = new Comment(delimiterText, CreateSpan(Current()));
            }
            Advance();
        }

        // Skip blank lines and collect directives/comments before request line
        var requestComments = new List<Comment>(leadingComments);
        if (delimiterComment != null)
        {
            requestComments.Add(delimiterComment);
        }

        while (!IsAtEnd())
        {
            var token = Current();
            if (token.Type == TokenType.BlankLine)
            {
                Advance();
                continue;
            }

            if (token.Type == TokenType.Comment)
            {
                requestComments.Add(ParseComment(token));
                Advance();
                continue;
            }

            if (token.Type == TokenType.Directive)
            {
                directives.Add(ParseDirective(token));
                Advance();
                continue;
            }

            break;
        }

        // Expect request line
        if (IsAtEnd() || Current().Type != TokenType.RequestLine)
        {
            // No request line found after delimiter
            if (!IsAtEnd())
            {
                AddDiagnostic(HttpDiagnosticSeverity.Warning, "Expected request line after delimiter", Current());
            }
            return null;
        }

        return ParseRequestFromLine(requestComments, directives, startToken);
    }

    private HttpRequest? ParseRequestFromLine(List<Comment> leadingComments, List<HttpDirective>? existingDirectives = null, Token? startToken = null)
    {
        startToken ??= Current();
        var directives = existingDirectives != null ? new List<HttpDirective>(existingDirectives) : [];

        // Parse request line
        var requestLineToken = Current();
        var (method, url, httpVersion) = ParseRequestLine(requestLineToken.Text);
        if (method == null || url == null)
        {
            AddDiagnostic(HttpDiagnosticSeverity.Error, "Invalid request line format", requestLineToken);
            Advance();
            return null;
        }
        Advance();

        // Handle URL continuation (lines starting with ? or &)
        var urlBuilder = new StringBuilder(url);
        while (!IsAtEnd() && IsUrlContinuation(Current()))
        {
            var continuationText = Current().Text.Trim().TrimEnd('\r', '\n');
            urlBuilder.Append(continuationText);
            Advance();
        }
        url = urlBuilder.ToString();

        // Parse headers
        var headers = new List<HttpHeader>();
        while (!IsAtEnd() && Current().Type == TokenType.Header)
        {
            var header = ParseHeader(Current());
            if (header != null)
            {
                headers.Add(header);
            }
            Advance();
        }

        // Skip blank line before body
        while (!IsAtEnd() && Current().Type == TokenType.BlankLine)
        {
            Advance();
        }

        // Parse body (everything until next delimiter or end)
        HttpRequestBody? body = null;
        var bodyLines = new List<Token>();
        while (!IsAtEnd() && Current().Type != TokenType.RequestDelimiter && Current().Type != TokenType.RequestLine)
        {
            var token = Current();
            if (token.Type == TokenType.VariableDefinition)
            {
                // Variable definition signals end of request body
                break;
            }

            // Check for directives that might appear mid-file
            if (token.Type == TokenType.Directive || token.Type == TokenType.Comment)
            {
                // Could be start of next request's comments
                if (LookAheadForRequest())
                {
                    break;
                }
            }

            bodyLines.Add(token);
            Advance();
        }

        if (bodyLines.Count > 0)
        {
            body = ParseBody(bodyLines, headers);
        }

        // Get request name from directives
        var name = directives.FirstOrDefault(d =>
            string.Equals(d.Name, HttpDirective.WellKnown.Name, StringComparison.OrdinalIgnoreCase))?.Value;

        var endToken = bodyLines.Count > 0 ? bodyLines[^1] : headers.Count > 0 ? new Token(
            TokenType.Header, "", headers[^1].Span.EndLine, headers[^1].Span.EndColumn, headers[^1].Span.EndOffset, headers[^1].Span.EndOffset
        ) : requestLineToken;

        var span = new SourceSpan(
            startToken.Value.Line,
            startToken.Value.Column,
            endToken.Line,
            endToken.Column + endToken.Text.TrimEnd('\r', '\n').Length,
            startToken.Value.StartOffset,
            endToken.EndOffset);

        return new HttpRequest(
            method,
            url,
            httpVersion,
            name,
            headers,
            body,
            directives,
            leadingComments,
            span);
    }

    private bool LookAheadForRequest()
    {
        var savedIndex = _tokenIndex;
        var depth = 0;
        while (_tokenIndex < _tokens.Count && depth < 10)
        {
            var token = _tokens[_tokenIndex];
            if (token.Type == TokenType.RequestLine || token.Type == TokenType.RequestDelimiter)
            {
                _tokenIndex = savedIndex;
                return true;
            }

            if (token.Type != TokenType.Comment && token.Type != TokenType.Directive &&
                token.Type != TokenType.BlankLine)
            {
                break;
            }

            _tokenIndex++;
            depth++;
        }

        _tokenIndex = savedIndex;
        return false;
    }

    private bool IsUrlContinuation(Token token)
    {
        if (token.Type != TokenType.BodyLine && token.Type != TokenType.Header)
        {
            return false;
        }

        var trimmed = token.Text.TrimStart();
        return trimmed.StartsWith("?", StringComparison.Ordinal) || trimmed.StartsWith("&", StringComparison.Ordinal);
    }

    private static (string? Method, string? Url, string? HttpVersion) ParseRequestLine(string text)
    {
        var line = text.Trim().TrimEnd('\r', '\n');
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return (null, null, null);
        }

        var method = parts[0].ToUpperInvariant();
        string? httpVersion = null;

        // Check if last part is HTTP version
        var urlEndIndex = parts.Length;
        if (parts.Length >= 3 && parts[parts.Length - 1].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            httpVersion = parts[parts.Length - 1];
            urlEndIndex = parts.Length - 1;
        }

        // URL might contain spaces (though unusual), join remaining parts
        var urlParts = new string[urlEndIndex - 1];
        Array.Copy(parts, 1, urlParts, 0, urlEndIndex - 1);
        var url = string.Join(" ", urlParts);

        return (method, url, httpVersion);
    }

    private HttpHeader? ParseHeader(Token token)
    {
        var text = token.Text.Trim().TrimEnd('\r', '\n');
        var colonIndex = text.IndexOf(':');
        if (colonIndex <= 0)
        {
            AddDiagnostic(HttpDiagnosticSeverity.Warning, "Invalid header format", token);
            return null;
        }

        var name = text.Substring(0, colonIndex).Trim();
        var value = text.Substring(colonIndex + 1).Trim();

        return new HttpHeader(name, value, CreateSpan(token));
    }

    private HttpRequestBody? ParseBody(List<Token> bodyTokens, IReadOnlyList<HttpHeader> headers)
    {
        if (bodyTokens.Count == 0)
        {
            return null;
        }

        // Trim leading and trailing blank lines
        while (bodyTokens.Count > 0 && bodyTokens[0].Type == TokenType.BlankLine)
        {
            bodyTokens.RemoveAt(0);
        }

        while (bodyTokens.Count > 0 && bodyTokens[^1].Type == TokenType.BlankLine)
        {
            bodyTokens.RemoveAt(bodyTokens.Count - 1);
        }

        if (bodyTokens.Count == 0)
        {
            return null;
        }

        var firstLineText = bodyTokens[0].Text.Trim();

        // Check for file reference (< path or <@ path)
        // Must be "< " or "<@" followed by path, not "<?xml" or "<tag>"
        if (IsFileReference(firstLineText))
        {
            return ParseFileReferenceBody(bodyTokens[0]);
        }

        // Check for multipart body
        var contentType = headers.FirstOrDefault(h =>
            string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))?.RawValue;
        if (contentType != null && contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            var boundary = ExtractBoundary(contentType);
            if (boundary != null)
            {
                return ParseMultipartBody(bodyTokens, boundary);
            }
        }

        // Regular text body
        var bodyContent = string.Join("", bodyTokens.Select(t => t.Text));
        // Trim trailing newlines
        bodyContent = bodyContent.TrimEnd('\r', '\n');

        var span = new SourceSpan(
            bodyTokens[0].Line,
            bodyTokens[0].Column,
            bodyTokens[^1].Line,
            bodyTokens[^1].Column + bodyTokens[^1].Text.TrimEnd('\r', '\n').Length,
            bodyTokens[0].StartOffset,
            bodyTokens[^1].EndOffset);

        return new TextBody(bodyContent, span);
    }

    private static bool IsFileReference(string text)
    {
        if (!text.StartsWith("<", StringComparison.Ordinal))
        {
            return false;
        }

        // File reference format: "< path" or "<@ path"
        // Must have space after < (or <@) to distinguish from XML/HTML
        if (text.StartsWith("<@"))
        {
            // <@ must be followed by space or path
            return text.Length > 2 && (text[2] == ' ' || text[2] == '.' || text[2] == '/' || text[2] == '\\' || char.IsLetter(text[2]));
        }

        if (text.Length > 1)
        {
            var secondChar = text[1];
            // "< path" - second char must be space, dot, slash, or letter (path start)
            // NOT: "<?xml", "<tag>", "<!DOCTYPE"
            return secondChar == ' ' || secondChar == '.' || secondChar == '/' || secondChar == '\\';
        }

        return false;
    }

    private FileReferenceBody ParseFileReferenceBody(Token token)
    {
        var text = token.Text.Trim().TrimEnd('\r', '\n');
        // Format: < path [encoding] or <@ path (with variable processing)
        var processVariables = text.StartsWith("<@", StringComparison.Ordinal);
        var pathStart = processVariables ? 2 : 1;
        var remaining = text.Substring(pathStart).Trim();

        string? encoding = null;
        var path = remaining;

        // Check for encoding specification
        var spaceIndex = remaining.LastIndexOf(' ');
        if (spaceIndex > 0)
        {
            var possibleEncoding = remaining.Substring(spaceIndex + 1);
            // Common encodings
            if (IsKnownEncoding(possibleEncoding))
            {
                encoding = possibleEncoding;
                path = remaining.Substring(0, spaceIndex).Trim();
            }
        }

        return new FileReferenceBody(path, encoding, processVariables, CreateSpan(token));
    }

    private static bool IsKnownEncoding(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower is "utf-8" or "utf8" or "utf-16" or "utf16" or "ascii" or "latin1" or "iso-8859-1";
    }

    private static string? ExtractBoundary(string contentType)
    {
        var match = BoundaryRegex().Match(contentType);
        return match.Success ? match.Groups[1].Value : null;
    }

    private MultipartBody ParseMultipartBody(List<Token> bodyTokens, string boundary)
    {
        var sections = new List<MultipartSection>();
        var content = string.Join("", bodyTokens.Select(t => t.Text));
        var delimiter = $"--{boundary}";
        var endDelimiter = $"--{boundary}--";

        var parts = content.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part) || part.Trim() == "--")
            {
                continue;
            }

            var section = ParseMultipartSection(part.TrimStart('\r', '\n'));
            if (section != null)
            {
                sections.Add(section);
            }
        }

        var lastToken = bodyTokens[bodyTokens.Count - 1];
        var span = new SourceSpan(
            bodyTokens[0].Line,
            bodyTokens[0].Column,
            lastToken.Line,
            lastToken.Column + lastToken.Text.TrimEnd('\r', '\n').Length,
            bodyTokens[0].StartOffset,
            lastToken.EndOffset);

        return new MultipartBody(boundary, sections, span);
    }

    private MultipartSection? ParseMultipartSection(string content)
    {
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var headers = new List<HttpHeader>();
        var bodyStartIndex = 0;

        // Parse headers until blank line
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                bodyStartIndex = i + 1;
                break;
            }

            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var name = line.Substring(0, colonIndex).Trim();
                var value = line.Substring(colonIndex + 1).Trim();
                headers.Add(new HttpHeader(name, value, SourceSpan.Empty));
            }
        }

        // Parse body
        HttpRequestBody? body = null;
        if (bodyStartIndex < lines.Length)
        {
            var bodyLines = new string[lines.Length - bodyStartIndex];
            Array.Copy(lines, bodyStartIndex, bodyLines, 0, lines.Length - bodyStartIndex);
            var bodyContent = string.Join("\n", bodyLines).TrimEnd('\r', '\n', '-');
            if (!string.IsNullOrWhiteSpace(bodyContent))
            {
                if (bodyContent.TrimStart().StartsWith("<", StringComparison.Ordinal))
                {
                    var token = new Token(TokenType.BodyLine, bodyContent, 0, 0, 0, bodyContent.Length);
                    body = ParseFileReferenceBody(token);
                }
                else
                {
                    body = new TextBody(bodyContent, SourceSpan.Empty);
                }
            }
        }

        return new MultipartSection(headers, body, SourceSpan.Empty);
    }

    private Comment ParseComment(Token token)
    {
        var text = token.Text.Trim().TrimEnd('\r', '\n');
        // Remove comment prefix
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            text = text.Substring(2).TrimStart();
        }
        else if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text.Substring(1).TrimStart();
        }

        return new Comment(text, CreateSpan(token));
    }

    private Comment ParseDirectiveAsComment(Token token)
    {
        var text = token.Text.Trim().TrimEnd('\r', '\n');
        return new Comment(text, CreateSpan(token));
    }

    private HttpDirective ParseDirective(Token token)
    {
        var text = token.Text.Trim().TrimEnd('\r', '\n');
        // Remove comment prefix first
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            text = text.Substring(2).TrimStart();
        }
        else if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text.Substring(1).TrimStart();
        }

        // Now parse @directive [value]
        if (!text.StartsWith("@", StringComparison.Ordinal))
        {
            return new HttpDirective(text, null, CreateSpan(token));
        }

        text = text.Substring(1); // Remove @
        var spaceIndex = text.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            return new HttpDirective(text, null, CreateSpan(token));
        }

        var name = text.Substring(0, spaceIndex);
        var value = text.Substring(spaceIndex + 1).Trim();

        return new HttpDirective(name, value, CreateSpan(token));
    }

    private FileVariable ParseVariableDefinition(Token token)
    {
        var text = token.Text.Trim().TrimEnd('\r', '\n');
        // Format: @name = value
        if (text.StartsWith("@", StringComparison.Ordinal))
        {
            text = text.Substring(1);
        }

        var equalsIndex = text.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return new FileVariable(text, "", CreateSpan(token));
        }

        var name = text.Substring(0, equalsIndex).Trim();
        var value = text.Substring(equalsIndex + 1).Trim();

        return new FileVariable(name, value, CreateSpan(token));
    }

    private static string ExtractDelimiterComment(string text)
    {
        var trimmed = text.Trim().TrimEnd('\r', '\n');
        if (trimmed.StartsWith("###", StringComparison.Ordinal))
        {
            return trimmed.Substring(3).Trim();
        }

        return "";
    }

    private SourceSpan CreateSpan(Token token)
    {
        var textLength = token.Text.TrimEnd('\r', '\n').Length;
        return new SourceSpan(
            token.Line,
            token.Column,
            token.Line,
            token.Column + textLength,
            token.StartOffset,
            token.EndOffset);
    }

    private void AddDiagnostic(HttpDiagnosticSeverity severity, string message, Token token)
    {
        _diagnostics.Add(new HttpDiagnostic(severity, message, CreateSpan(token)));
    }

    private Token Current() => _tokenIndex < _tokens.Count ? _tokens[_tokenIndex] : new Token(TokenType.EOF, "", 0, 0, 0, 0);
    private void Advance() => _tokenIndex++;
    private bool IsAtEnd() => _tokenIndex >= _tokens.Count || _tokens[_tokenIndex].Type == TokenType.EOF;

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"boundary=([^;\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BoundaryRegex();
#else
    private static readonly Regex _boundaryRegex = new(@"boundary=([^;\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex BoundaryRegex() => _boundaryRegex;
#endif
}
