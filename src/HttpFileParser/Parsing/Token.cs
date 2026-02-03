namespace HttpFileParser.Parsing;

/// <summary>
/// Token types produced by the HTTP file lexer.
/// </summary>
public enum TokenType
{
    RequestDelimiter,
    Comment,
    VariableDefinition,
    Directive,
    RequestLine,
    Header,
    BodyLine,
    BlankLine,
    EOF
}

/// <summary>
/// Represents a token from the HTTP file lexer.
/// </summary>
public readonly struct Token
{
    public TokenType Type { get; }
    public string Text { get; }
    public int Line { get; }
    public int Column { get; }
    public int StartOffset { get; }
    public int EndOffset { get; }

    public Token(TokenType type, string text, int line, int column, int startOffset, int endOffset)
    {
        Type = type;
        Text = text;
        Line = line;
        Column = column;
        StartOffset = startOffset;
        EndOffset = endOffset;
    }

    public int Length => EndOffset - StartOffset;

    public override string ToString() => $"{Type}({Line},{Column}): \"{Text.Replace("\r", "\\r").Replace("\n", "\\n")}\"";
}
