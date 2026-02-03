namespace HttpFileParser.Model;

/// <summary>
/// Represents a diagnostic message (warning or error) from parsing.
/// </summary>
public sealed class HttpDiagnostic
{
    public HttpDiagnosticSeverity Severity { get; }
    public string Message { get; }
    public SourceSpan Span { get; }
    public string? Code { get; }

    public HttpDiagnostic(HttpDiagnosticSeverity severity, string message, SourceSpan span, string? code = null)
    {
        Severity = severity;
        Message = message;
        Span = span;
        Code = code;
    }

    public override string ToString()
    {
        var codeStr = Code is not null ? $" {Code}:" : "";
        return $"{Severity}{codeStr} ({Span.StartLine},{Span.StartColumn}): {Message}";
    }
}

public enum HttpDiagnosticSeverity
{
    Warning,
    Error
}
