using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Execution;

/// <summary>
/// Represents a resolved HTTP request with all variables expanded.
/// </summary>
public sealed class ResolvedHttpRequest
{
    public HttpRequest OriginalRequest { get; }
    public string Method { get; }
    public string ResolvedUrl { get; }
    public IReadOnlyList<KeyValuePair<string, string>> ResolvedHeaders { get; }
    public string? ResolvedBody { get; }
    public IReadOnlyList<string> UnresolvedVariables { get; }

    public ResolvedHttpRequest(
        HttpRequest originalRequest,
        string method,
        string resolvedUrl,
        IReadOnlyList<KeyValuePair<string, string>> resolvedHeaders,
        string? resolvedBody,
        IReadOnlyList<string> unresolvedVariables)
    {
        OriginalRequest = originalRequest;
        Method = method;
        ResolvedUrl = resolvedUrl;
        ResolvedHeaders = resolvedHeaders;
        ResolvedBody = resolvedBody;
        UnresolvedVariables = unresolvedVariables;
    }

    public bool HasUnresolvedVariables => UnresolvedVariables.Count > 0;
}
