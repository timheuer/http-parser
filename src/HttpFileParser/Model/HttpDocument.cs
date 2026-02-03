namespace HttpFileParser.Model;

/// <summary>
/// Root container for a parsed HTTP document.
/// </summary>
public sealed class HttpDocument
{
    public string? FilePath { get; }
    public IReadOnlyList<HttpDocumentItem> Items { get; }
    public IReadOnlyList<HttpDiagnostic> Diagnostics { get; }

    public HttpDocument(
        string? filePath,
        IReadOnlyList<HttpDocumentItem> items,
        IReadOnlyList<HttpDiagnostic> diagnostics)
    {
        FilePath = filePath;
        Items = items;
        Diagnostics = diagnostics;
    }

    public IEnumerable<HttpRequest> Requests => Items.OfType<HttpRequest>();

    public IEnumerable<FileVariable> Variables => Items.OfType<FileVariable>();

    public IEnumerable<Comment> Comments => Items.OfType<Comment>();

    public HttpRequest? GetRequestByName(string name)
    {
        return Requests.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == HttpDiagnosticSeverity.Error);

    public bool HasWarnings => Diagnostics.Any(d => d.Severity == HttpDiagnosticSeverity.Warning);
}
