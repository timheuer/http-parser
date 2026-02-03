namespace HttpFileParser.Model;

/// <summary>
/// Represents an HTTP request in the document.
/// </summary>
public sealed class HttpRequest : HttpDocumentItem
{
    public string Method { get; }
    public string RawUrl { get; }
    public string? HttpVersion { get; }
    public string? Name { get; }
    public IReadOnlyList<HttpHeader> Headers { get; }
    public HttpRequestBody? Body { get; }
    public IReadOnlyList<HttpDirective> Directives { get; }
    public IReadOnlyList<Comment> LeadingComments { get; }

    public HttpRequest(
        string method,
        string rawUrl,
        string? httpVersion,
        string? name,
        IReadOnlyList<HttpHeader> headers,
        HttpRequestBody? body,
        IReadOnlyList<HttpDirective> directives,
        IReadOnlyList<Comment> leadingComments,
        SourceSpan span) : base(span)
    {
        Method = method;
        RawUrl = rawUrl;
        HttpVersion = httpVersion;
        Name = name;
        Headers = headers;
        Body = body;
        Directives = directives;
        LeadingComments = leadingComments;
    }

    public bool HasDirective(string directiveName)
    {
        return Directives.Any(d => string.Equals(d.Name, directiveName, StringComparison.OrdinalIgnoreCase));
    }

    public string? GetDirectiveValue(string directiveName)
    {
        return Directives.FirstOrDefault(d => string.Equals(d.Name, directiveName, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    public bool IsGraphQL => Headers.Any(h =>
        string.Equals(h.Name, "X-REQUEST-TYPE", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(h.RawValue.Trim(), "GraphQL", StringComparison.OrdinalIgnoreCase));
}
