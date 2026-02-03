namespace HttpFileParser.Model;

/// <summary>
/// Base class for all items in an HTTP document.
/// </summary>
public abstract class HttpDocumentItem
{
    public SourceSpan Span { get; }

    protected HttpDocumentItem(SourceSpan span)
    {
        Span = span;
    }
}
