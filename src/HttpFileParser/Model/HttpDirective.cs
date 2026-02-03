namespace HttpFileParser.Model;

/// <summary>
/// Represents a directive applied to a request (e.g., @no-redirect, @note).
/// </summary>
public sealed class HttpDirective
{
    public string Name { get; }
    public string? Value { get; }
    public SourceSpan Span { get; }

    public HttpDirective(string name, string? value, SourceSpan span)
    {
        Name = name;
        Value = value;
        Span = span;
    }

    public static class WellKnown
    {
        public const string Name = "name";
        public const string NoRedirect = "no-redirect";
        public const string Note = "note";
        public const string NoCookieJar = "no-cookie-jar";
        public const string Prompt = "prompt";
    }
}
