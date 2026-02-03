namespace HttpFileParser.Model;

/// <summary>
/// Represents an HTTP header with name and value.
/// </summary>
public sealed class HttpHeader
{
    public string Name { get; }
    public string RawValue { get; }
    public SourceSpan Span { get; }

    public HttpHeader(string name, string rawValue, SourceSpan span)
    {
        Name = name;
        RawValue = rawValue;
        Span = span;
    }
}
