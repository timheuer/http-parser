namespace HttpFileParser.Model;

/// <summary>
/// Represents a comment in an HTTP document.
/// </summary>
public sealed class Comment : HttpDocumentItem
{
    public string Text { get; }

    public Comment(string text, SourceSpan span) : base(span)
    {
        Text = text;
    }
}
