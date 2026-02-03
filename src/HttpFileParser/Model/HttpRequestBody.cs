namespace HttpFileParser.Model;

/// <summary>
/// Base class for HTTP request body types.
/// </summary>
public abstract class HttpRequestBody
{
    public SourceSpan Span { get; }

    protected HttpRequestBody(SourceSpan span)
    {
        Span = span;
    }
}

/// <summary>
/// Represents a text body (JSON, XML, plain text, etc.).
/// </summary>
public sealed class TextBody : HttpRequestBody
{
    public string Content { get; }

    public TextBody(string content, SourceSpan span) : base(span)
    {
        Content = content;
    }
}

/// <summary>
/// Represents a file reference body (< ./path/to/file).
/// </summary>
public sealed class FileReferenceBody : HttpRequestBody
{
    public string FilePath { get; }
    public string? Encoding { get; }
    public bool ProcessVariables { get; }

    public FileReferenceBody(string filePath, string? encoding, bool processVariables, SourceSpan span) : base(span)
    {
        FilePath = filePath;
        Encoding = encoding;
        ProcessVariables = processVariables;
    }
}

/// <summary>
/// Represents a multipart form body.
/// </summary>
public sealed class MultipartBody : HttpRequestBody
{
    public string Boundary { get; }
    public IReadOnlyList<MultipartSection> Sections { get; }

    public MultipartBody(string boundary, IReadOnlyList<MultipartSection> sections, SourceSpan span) : base(span)
    {
        Boundary = boundary;
        Sections = sections;
    }
}

/// <summary>
/// Represents a section within a multipart body.
/// </summary>
public sealed class MultipartSection
{
    public IReadOnlyList<HttpHeader> Headers { get; }
    public HttpRequestBody? Body { get; }
    public SourceSpan Span { get; }

    public MultipartSection(IReadOnlyList<HttpHeader> headers, HttpRequestBody? body, SourceSpan span)
    {
        Headers = headers;
        Body = body;
        Span = span;
    }
}
